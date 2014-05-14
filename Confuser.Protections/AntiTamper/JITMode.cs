using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using Confuser.Core.Services;
using dnlib.DotNet.Writer;
using dnlib.DotNet.Emit;
using Confuser.Core.Helpers;
using System.Diagnostics;
using Confuser.Renamer;
using System.IO;

namespace Confuser.Protections.AntiTamper
{
    class JITMode : IModeHandler
    {
        RandomGenerator random;
        uint z, x, c, v;
        uint name1, name2;
        uint key;
        IKeyDeriver deriver;

        MethodDef initMethod;
        MethodDef cctor;
        MethodDef cctorRepl;
        List<MethodDef> methods;
        public void HandleInject(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters)
        {
            random = context.Registry.GetService<IRandomService>().GetRandomGenerator(parent.FullId);
            z = random.NextUInt32();
            x = random.NextUInt32();
            c = random.NextUInt32();
            v = random.NextUInt32();
            name1 = random.NextUInt32() & 0x7f7f7f7f;
            name2 = random.NextUInt32() & 0x7f7f7f7f;
            key = random.NextUInt32();

            switch (parameters.GetParameter<Mode>(context, context.CurrentModule, "key", Mode.Normal))
            {
                case Mode.Normal:
                    deriver = new NormalDeriver();
                    break;
                case Mode.Dynamic:
                    deriver = new DynamicDeriver();
                    break;
                default:
                    throw new UnreachableException();
            }
            deriver.Init(context, random);

            var rt = context.Registry.GetService<IRuntimeService>();
            var initType = rt.GetRuntimeType("Confuser.Runtime.AntiTamperJIT");
            var defs = InjectHelper.Inject(initType, context.CurrentModule.GlobalType, context.CurrentModule);
            initMethod = defs.OfType<MethodDef>().Single(method => method.Name == "Initialize");

            initMethod.Body.SimplifyMacros(initMethod.Parameters);
            var instrs = initMethod.Body.Instructions.ToList();
            for (int i = 0; i < instrs.Count; i++)
            {
                var instr = instrs[i];
                if (instr.OpCode == OpCodes.Ldtoken)
                {
                    instr.Operand = context.CurrentModule.GlobalType;
                }
                else if (instr.OpCode == OpCodes.Call)
                {
                    IMethod method = (IMethod)instr.Operand;
                    if (method.DeclaringType.Name == "Mutation" &&
                       method.Name == "Crypt")
                    {
                        Instruction ldDst = instrs[i - 2];
                        Instruction ldSrc = instrs[i - 1];
                        Debug.Assert(ldDst.OpCode == OpCodes.Ldloc && ldSrc.OpCode == OpCodes.Ldloc);
                        instrs.RemoveAt(i);
                        instrs.RemoveAt(i - 1);
                        instrs.RemoveAt(i - 2);
                        instrs.InsertRange(i - 2, deriver.EmitDerivation(initMethod, context, (Local)ldDst.Operand, (Local)ldSrc.Operand));
                    }
                }
            }
            initMethod.Body.Instructions.Clear();
            foreach (var instr in instrs)
                initMethod.Body.Instructions.Add(instr);

            MutationHelper.InjectKeys(initMethod,
                new int[] { 0, 1, 2, 3, 4 },
                new int[] { (int)(name1 * name2), (int)z, (int)x, (int)c, (int)v });

            var name = context.Registry.GetService<INameService>();
            var marker = context.Registry.GetService<IMarkerService>();

            cctor = context.CurrentModule.GlobalType.FindStaticConstructor();

            cctorRepl = new MethodDefUser(name.RandomName(), MethodSig.CreateStatic(context.CurrentModule.CorLibTypes.Void));
            cctorRepl.IsStatic = true;
            cctorRepl.Access = MethodAttributes.CompilerControlled;
            cctorRepl.Body = new CilBody();
            cctorRepl.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            context.CurrentModule.GlobalType.Methods.Add(cctorRepl);
            name.MarkHelper(cctorRepl, marker);

            MutationHelper.InjectKeys(defs.OfType<MethodDef>().Single(method => method.Name == "HookHandler"),
                 new int[] { 0 }, new int[] { (int)key });
            foreach (var def in defs)
            {
                name.MarkHelper(def, marker);
                if (def is MethodDef)
                    parent.ExcludeMethod(context, (MethodDef)def);
            }
            parent.ExcludeMethod(context, cctor);
        }

        public void HandleMD(AntiTamperProtection parent, ConfuserContext context, ProtectionParameters parameters)
        {
            methods = parameters.Targets.OfType<MethodDef>().Where(method => method.HasBody).ToList();
            context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
        }

        static readonly dnlib.DotNet.Emit.CilBody NopBody = new dnlib.DotNet.Emit.CilBody()
        {
            Instructions =
            {
                Instruction.Create(OpCodes.Ldnull),
                Instruction.Create(OpCodes.Throw)
            }
        };

        void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e)
        {
            var writer = (ModuleWriter)sender;
            if (e.WriterEvent == ModuleWriterEvent.MDBeginWriteMethodBodies)
            {
                CreateSection(writer);
            }
            else if (e.WriterEvent == ModuleWriterEvent.BeginStrongNameSign)
            {
                EncryptSection(writer);
            }
        }

        void CreateSection(ModuleWriter writer)
        {
            // move some PE parts to separate section to prevent it from being hashed
            var peSection = new PESection("", 0x60000020);
            bool moved = false;
            uint alignment;
            if (writer.StrongNameSignature != null)
            {
                alignment = writer.TextSection.Remove(writer.StrongNameSignature).Value;
                peSection.Add(writer.StrongNameSignature, alignment);
                moved = true;
            }
            if (writer.ImportAddressTable != null)
            {
                alignment = writer.TextSection.Remove(writer.ImportAddressTable).Value;
                peSection.Add(writer.ImportAddressTable, alignment);
                moved = true;
            }
            if (writer.StartupStub != null)
            {
                alignment = writer.TextSection.Remove(writer.StartupStub).Value;
                peSection.Add(writer.StartupStub, alignment);
                moved = true;
            }
            if (moved)
                writer.Sections.Add(peSection);

            // create section
            byte[] nameBuffer = new byte[8];
            nameBuffer[0] = (byte)(name1 >> 0);
            nameBuffer[1] = (byte)(name1 >> 8);
            nameBuffer[2] = (byte)(name1 >> 16);
            nameBuffer[3] = (byte)(name1 >> 24);
            nameBuffer[4] = (byte)(name2 >> 0);
            nameBuffer[5] = (byte)(name2 >> 8);
            nameBuffer[6] = (byte)(name2 >> 16);
            nameBuffer[7] = (byte)(name2 >> 24);
            var newSection = new PESection(Encoding.ASCII.GetString(nameBuffer), 0xE0000040);
            writer.Sections.Insert(random.NextInt32(writer.Sections.Count), newSection);

            // random padding at beginning to prevent revealing hash key
            newSection.Add(new ByteArrayChunk(random.NextBytes(0x10)), 0x10);

            // create index
            JITBodyIndex bodyIndex = new JITBodyIndex(methods.Select(method => writer.MetaData.GetToken(method).Raw));
            newSection.Add(bodyIndex, 0x10);

            // move initialization away from module initializer
            cctorRepl.Body = cctor.Body;
            cctor.Body = new CilBody();
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, initMethod));
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, cctorRepl));
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            // save methods
            foreach (var method in methods)
            {
                if (!method.HasBody)
                    continue;

                MDToken token = writer.MetaData.GetToken(method);

                var jitBody = new JITMethodBody();
                var bodyWriter = new JITMethodBodyWriter(writer.MetaData, method.Body, jitBody, random.NextUInt32(), writer.MetaData.KeepOldMaxStack || method.Body.KeepOldMaxStack);
                bodyWriter.Write();
                jitBody.Serialize(token.Raw, key);
                bodyIndex.Add(token.Raw, jitBody);

                method.Body = NopBody;
                writer.MetaData.TablesHeap.MethodTable[token.Rid].ImplFlags |= (ushort)MethodImplAttributes.NoInlining;

            }
            bodyIndex.PopulateSection(newSection);

            // padding to prevent bad size due to shift division
            newSection.Add(new ByteArrayChunk(new byte[4]), 4);
        }

        void EncryptSection(ModuleWriter writer)
        {
            var stream = writer.DestinationStream;
            var reader = new BinaryReader(writer.DestinationStream);
            stream.Position = 0x3C;
            stream.Position = reader.ReadUInt32();

            stream.Position += 6;
            ushort sections = reader.ReadUInt16();
            stream.Position += 0xc;
            ushort optSize = reader.ReadUInt16();
            stream.Position += 2 + optSize;

            uint encLoc = 0, encSize = 0;
            for (int i = 0; i < sections; i++)
            {
                uint nameHash = reader.ReadUInt32() * reader.ReadUInt32();
                stream.Position += 8;
                if (nameHash == name1 * name2)
                {
                    encSize = reader.ReadUInt32();
                    encLoc = reader.ReadUInt32();
                }
                else if (nameHash != 0)
                {
                    uint sectSize = reader.ReadUInt32();
                    uint sectLoc = reader.ReadUInt32();
                    Hash(stream, reader, sectLoc, sectSize);
                }
                stream.Position += 16;
            }

            uint[] key = DeriveKey();
            encSize >>= 2;
            stream.Position = encLoc;
            uint[] result = new uint[encSize];
            for (uint i = 0; i < encSize; i++)
            {
                uint data = reader.ReadUInt32();
                result[i] = data ^ key[i & 0xf];
                key[i & 0xf] = (key[i & 0xf] ^ data) + 0x3dbb2819;
            }
            byte[] byteResult = new byte[encSize << 2];
            Buffer.BlockCopy(result, 0, byteResult, 0, byteResult.Length);
            stream.Position = encLoc;
            stream.Write(byteResult, 0, byteResult.Length);
        }

        void Hash(Stream stream, BinaryReader reader, uint offset, uint size)
        {
            long original = stream.Position;
            stream.Position = offset;
            size >>= 2;
            for (uint i = 0; i < size; i++)
            {
                uint data = reader.ReadUInt32();
                uint tmp = (z ^ data) + x + c * v;
                z = x;
                x = c;
                x = v;
                v = tmp;
            }
            stream.Position = original;
        }

        uint[] DeriveKey()
        {
            uint[] dst = new uint[0x10], src = new uint[0x10];
            for (int i = 0; i < 0x10; i++)
            {
                dst[i] = v;
                src[i] = x;
                z = (x >> 5) | (x << 27);
                x = (c >> 3) | (c << 29);
                c = (v >> 7) | (v << 25);
                v = (z >> 11) | (z << 21);
            }
            return deriver.DeriveKey(dst, src);
        }
    }
}
