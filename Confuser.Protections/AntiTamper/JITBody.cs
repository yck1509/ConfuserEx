using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System.IO;
using System.Diagnostics;
using dnlib.IO;
using dnlib.PE;
using dnlib.DotNet;

namespace Confuser.Protections.AntiTamper
{
    struct JITEHClause
    {
        public uint Flags;
        public uint TryOffset;
        public uint TryLength;
        public uint HandlerOffset;
        public uint HandlerLength;
        public uint ClassTokenOrFilterOffset;
    }

    class JITMethodBody : IChunk
    {
        public byte[] ILCode;
        public uint MaxStack;
        public JITEHClause[] EHs;
        public byte[] LocalVars;
        public uint Options;
        public uint MulSeed;

        public uint Offset;
        public byte[] Body;

        public void Serialize(uint token, uint key, byte[] fieldLayout)
        {
            using (var ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                foreach (var i in fieldLayout)
                    switch (i)
                    {
                        case 0: writer.Write((uint)ILCode.Length); break;
                        case 1: writer.Write(MaxStack); break;
                        case 2: writer.Write((uint)EHs.Length); break;
                        case 3: writer.Write((uint)LocalVars.Length); break;
                        case 4: writer.Write(Options); break;
                        case 5: writer.Write(MulSeed); break;
                    }

                writer.Write(ILCode);
                writer.Write(LocalVars);
                foreach (var clause in EHs)
                {
                    writer.Write(clause.Flags);
                    writer.Write(clause.TryOffset);
                    writer.Write(clause.TryLength);
                    writer.Write(clause.HandlerOffset);
                    writer.Write(clause.HandlerLength);
                    writer.Write(clause.ClassTokenOrFilterOffset);
                }
                writer.WriteZeros(4 - ((int)ms.Length & 3));    // pad to 4 bytes
                Body = ms.ToArray();
            }
            Debug.Assert(Body.Length % 4 == 0);
            // encrypt body
            uint state = token * key;
            uint counter = state;
            for (uint i = 0; i < Body.Length; i+=4)
            {
                uint data = Body[i] | (uint)(Body[i + 1] << 8) | (uint)(Body[i + 2] << 16) | (uint)(Body[i + 3] << 24);
                Body[i + 0] ^= (byte)(state >> 0);
                Body[i + 1] ^= (byte)(state >> 8);
                Body[i + 2] ^= (byte)(state >> 16);
                Body[i + 3] ^= (byte)(state >> 24);
                state += data ^ counter;
                counter ^= (state >> 5) | (state << 27);
            }
        }

        FileOffset offset;
        RVA rva;

        public FileOffset FileOffset { get { return offset; } }

        public RVA RVA { get { return rva; } }

        public void SetOffset(FileOffset offset, RVA rva)
        {
            this.offset = offset;
            this.rva = rva;
        }

        public uint GetFileLength()
        {
            return (uint)Body.Length + 4;
        }

        public uint GetVirtualSize()
        {
            return GetFileLength();
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write((uint)(Body.Length >> 2));
            writer.Write(Body);
        }
    }

    class JITMethodBodyWriter : MethodBodyWriterBase
    {
        MetaData metadata;
        CilBody body;
        JITMethodBody jitBody;
        bool keepMaxStack;

        public JITMethodBodyWriter(MetaData md, CilBody body, JITMethodBody jitBody, uint mulSeed, bool keepMaxStack) :
            base(body.Instructions, body.ExceptionHandlers)
        {
            this.metadata = md;
            this.body = body;
            this.jitBody = jitBody;
            this.keepMaxStack = keepMaxStack;
            this.jitBody.MulSeed = mulSeed;
        }

        public void Write()
        {
            uint codeSize = InitializeInstructionOffsets();
            jitBody.MaxStack = keepMaxStack ? body.MaxStack : GetMaxStack();

            jitBody.Options = 0;
            if (body.InitLocals)
                jitBody.Options |= 0x10;

            if (body.Variables.Count > 0)
            {
                LocalSig local = new LocalSig(body.Variables.Select(var => var.Type).ToList());
                jitBody.LocalVars = SignatureWriter.Write(metadata, local);
            }
            else
                jitBody.LocalVars = new byte[0];

            using (var ms = new MemoryStream())
            {
                uint _codeSize = WriteInstructions(new BinaryWriter(ms));
                Debug.Assert(codeSize == _codeSize);
                jitBody.ILCode = ms.ToArray();
            }

            jitBody.EHs = new JITEHClause[exceptionHandlers.Count];
            if (exceptionHandlers.Count > 0)
            {
                jitBody.Options |= 8;
                for (int i = 0; i < exceptionHandlers.Count; i++)
                {
                    var eh = exceptionHandlers[i];
                    jitBody.EHs[i].Flags = (uint)eh.HandlerType;

                    uint tryStart = GetOffset(eh.TryStart);
                    uint tryEnd = GetOffset(eh.TryEnd);
                    jitBody.EHs[i].TryOffset = tryStart;
                    jitBody.EHs[i].TryLength = tryEnd - tryStart;

                    uint handlerStart = GetOffset(eh.HandlerStart);
                    uint handlerEnd = GetOffset(eh.HandlerEnd);
                    jitBody.EHs[i].HandlerOffset = handlerStart;
                    jitBody.EHs[i].HandlerLength = handlerEnd - handlerStart;

                    if (eh.HandlerType == ExceptionHandlerType.Catch)
                    {
                        var token = metadata.GetToken(eh.CatchType).Raw;
                        if ((token & 0xff000000) == 0x1b000000)
                            jitBody.Options |= 0x80;

                        jitBody.EHs[i].ClassTokenOrFilterOffset = token;
                    }
                    else if (eh.HandlerType == ExceptionHandlerType.Filter)
                    {
                        jitBody.EHs[i].ClassTokenOrFilterOffset = GetOffset(eh.FilterStart);
                    }
                }
            }
        }

        protected override void WriteInlineField(BinaryWriter writer, Instruction instr)
        {
            writer.Write(metadata.GetToken(instr.Operand).Raw);
        }

        protected override void WriteInlineMethod(BinaryWriter writer, Instruction instr)
        {
            writer.Write(metadata.GetToken(instr.Operand).Raw);
        }

        protected override void WriteInlineSig(BinaryWriter writer, Instruction instr)
        {
            writer.Write(metadata.GetToken(instr.Operand).Raw);
        }

        protected override void WriteInlineString(BinaryWriter writer, Instruction instr)
        {
            writer.Write(metadata.GetToken(instr.Operand).Raw);
        }

        protected override void WriteInlineTok(BinaryWriter writer, Instruction instr)
        {
            writer.Write(metadata.GetToken(instr.Operand).Raw);
        }

        protected override void WriteInlineType(BinaryWriter writer, Instruction instr)
        {
            writer.Write(metadata.GetToken(instr.Operand).Raw);
        }
    }

    class JITBodyIndex : IChunk
    {
        Dictionary<uint, JITMethodBody> bodies;

        public JITBodyIndex(IEnumerable<uint> tokens)
        {
            bodies = tokens.ToDictionary(token => token, token => (JITMethodBody)null);
        }

        public void Add(uint token, JITMethodBody body)
        {
            Debug.Assert(bodies.ContainsKey(token));
            bodies[token] = body;
        }

        public void PopulateSection(PESection section)
        {
            uint offset = 0;
            foreach (var entry in bodies.OrderBy(entry => entry.Key))
            {
                Debug.Assert(entry.Value != null);
                section.Add(entry.Value, 4);
                entry.Value.Offset = offset;

                Debug.Assert(entry.Value.GetFileLength() % 4 == 0);
                offset += entry.Value.GetFileLength();
            }
        }

        FileOffset offset;
        RVA rva;

        public FileOffset FileOffset { get { return offset; } }

        public RVA RVA { get { return rva; } }

        public void SetOffset(FileOffset offset, RVA rva)
        {
            this.offset = offset;
            this.rva = rva;
        }

        public uint GetFileLength()
        {
            return (uint)bodies.Count * 8 + 4;
        }

        public uint GetVirtualSize()
        {
            return GetFileLength();
        }

        public void WriteTo(BinaryWriter writer)
        {
            uint length = GetFileLength() - 4;  // minus length field
            writer.Write((uint)bodies.Count);
            foreach (var entry in bodies.OrderBy(entry => entry.Key))
            {
                writer.Write(entry.Key);
                Debug.Assert(entry.Value != null);
                Debug.Assert((length + entry.Value.Offset) % 4 == 0);
                writer.Write((length + entry.Value.Offset) >> 2);
            }
        }
    }
}