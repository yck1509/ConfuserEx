using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using Confuser.Protections.Compress;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;
using dnlib.PE;
using FileAttributes = dnlib.DotNet.FileAttributes;
using SR = System.Reflection;

namespace Confuser.Protections {
	internal class Compressor : Packer {
		public const string _Id = "compressor";
		public const string _FullId = "Ki.Compressor";
		public const string _ServiceId = "Ki.Compressor";
		public static readonly object ContextKey = new object();

		public override string Name {
			get { return "Compressing Packer"; }
		}

		public override string Description {
			get { return "This packer reduces the size of output."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		protected override void Initialize(ConfuserContext context) { }

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.WriteModule, new ExtractPhase(this));
		}

		protected override void Pack(ConfuserContext context, ProtectionParameters parameters) {
			var ctx = context.Annotations.Get<CompressorContext>(context, ContextKey);
			if (ctx == null) {
				context.Logger.Error("No executable module!");
				throw new ConfuserException(null);
			}

			ModuleDefMD originModule = context.Modules[ctx.ModuleIndex];
			ctx.OriginModuleDef = originModule;

			var stubModule = new ModuleDefUser(ctx.ModuleName, originModule.Mvid, originModule.CorLibTypes.AssemblyRef);
			if (ctx.CompatMode) {
				var assembly = new AssemblyDefUser(originModule.Assembly);
				assembly.Name += ".cr";
				assembly.Modules.Add(stubModule);
			}
			else {
				ctx.Assembly.Modules.Insert(0, stubModule);
				ImportAssemblyTypeReferences(originModule, stubModule);
			}
			stubModule.Characteristics = originModule.Characteristics;
			stubModule.Cor20HeaderFlags = originModule.Cor20HeaderFlags;
			stubModule.Cor20HeaderRuntimeVersion = originModule.Cor20HeaderRuntimeVersion;
			stubModule.DllCharacteristics = originModule.DllCharacteristics;
			stubModule.EncBaseId = originModule.EncBaseId;
			stubModule.EncId = originModule.EncId;
			stubModule.Generation = originModule.Generation;
			stubModule.Kind = ctx.Kind;
			stubModule.Machine = originModule.Machine;
			stubModule.RuntimeVersion = originModule.RuntimeVersion;
			stubModule.TablesHeaderVersion = originModule.TablesHeaderVersion;
			stubModule.Win32Resources = originModule.Win32Resources;

			InjectStub(context, ctx, parameters, stubModule);

			var snKey = context.Annotations.Get<StrongNameKey>(originModule, Marker.SNKey);
			using (var ms = new MemoryStream()) {
				stubModule.Write(ms, new ModuleWriterOptions(stubModule, new KeyInjector(ctx)) {
					StrongNameKey = snKey
				});
				context.CheckCancellation();
				ProtectStub(context, context.OutputPaths[ctx.ModuleIndex], ms.ToArray(), snKey, new StubProtection(ctx, originModule));
			}
		}

		static string GetId(byte[] module) {
			var md = MetaDataCreator.CreateMetaData(new PEImage(module));
			var assemblyRow = md.TablesStream.ReadAssemblyRow(1);
			var assembly = new AssemblyNameInfo();
			assembly.Name = md.StringsStream.ReadNoNull(assemblyRow.Name);
			assembly.Culture = md.StringsStream.ReadNoNull(assemblyRow.Locale);
			assembly.PublicKeyOrToken = new PublicKey(md.BlobStream.Read(assemblyRow.PublicKey));
			assembly.HashAlgId = (AssemblyHashAlgorithm)assemblyRow.HashAlgId;
			assembly.Version = new Version(assemblyRow.MajorVersion, assemblyRow.MinorVersion, assemblyRow.BuildNumber, assemblyRow.RevisionNumber);
			assembly.Attributes = (AssemblyAttributes)assemblyRow.Flags;
			return GetId(assembly);
		}

		static string GetId(IAssembly assembly) {
			return new SR.AssemblyName(assembly.FullName).FullName.ToUpperInvariant();
		}

		void PackModules(ConfuserContext context, CompressorContext compCtx, ModuleDef stubModule, ICompressionService comp, RandomGenerator random) {
			int maxLen = 0;
			var modules = new Dictionary<string, byte[]>();
			for (int i = 0; i < context.OutputModules.Count; i++) {
				if (i == compCtx.ModuleIndex)
					continue;

				string id = GetId(context.Modules[i].Assembly);
				modules.Add(id, context.OutputModules[i]);

				int strLen = Encoding.UTF8.GetByteCount(id);
				if (strLen > maxLen)
					maxLen = strLen;
			}
			foreach (var extModule in context.ExternalModules) {
				var name = GetId(extModule).ToUpperInvariant();
				modules.Add(name, extModule);

				int strLen = Encoding.UTF8.GetByteCount(name);
				if (strLen > maxLen)
					maxLen = strLen;
			}

			byte[] key = random.NextBytes(4 + maxLen);
			key[0] = (byte)(compCtx.EntryPointToken >> 0);
			key[1] = (byte)(compCtx.EntryPointToken >> 8);
			key[2] = (byte)(compCtx.EntryPointToken >> 16);
			key[3] = (byte)(compCtx.EntryPointToken >> 24);
			for (int i = 4; i < key.Length; i++) // no zero bytes
				key[i] |= 1;
			compCtx.KeySig = key;

			int moduleIndex = 0;
			foreach (var entry in modules) {
				byte[] name = Encoding.UTF8.GetBytes(entry.Key);
				for (int i = 0; i < name.Length; i++)
					name[i] *= key[i + 4];

				uint state = 0x6fff61;
				foreach (byte chr in name)
					state = state * 0x5e3f1f + chr;
				byte[] encrypted = compCtx.Encrypt(comp, entry.Value, state, progress => {
					progress = (progress + moduleIndex) / modules.Count;
					context.Logger.Progress((int)(progress * 10000), 10000);
				});
				context.CheckCancellation();

				var resource = new EmbeddedResource(Convert.ToBase64String(name), encrypted, ManifestResourceAttributes.Private);
				stubModule.Resources.Add(resource);
				moduleIndex++;
			}
			context.Logger.EndProgress();
		}

		void InjectData(ModuleDef stubModule, MethodDef method, byte[] data) {
			var dataType = new TypeDefUser("", "DataType", stubModule.CorLibTypes.GetTypeRef("System", "ValueType"));
			dataType.Layout = TypeAttributes.ExplicitLayout;
			dataType.Visibility = TypeAttributes.NestedPrivate;
			dataType.IsSealed = true;
			dataType.ClassLayout = new ClassLayoutUser(1, (uint)data.Length);
			stubModule.GlobalType.NestedTypes.Add(dataType);

			var dataField = new FieldDefUser("DataField", new FieldSig(dataType.ToTypeSig())) {
				IsStatic = true,
				HasFieldRVA = true,
				InitialValue = data,
				Access = FieldAttributes.CompilerControlled
			};
			stubModule.GlobalType.Fields.Add(dataField);

			MutationHelper.ReplacePlaceholder(method, arg => {
				var repl = new List<Instruction>();
				repl.AddRange(arg);
				repl.Add(Instruction.Create(OpCodes.Dup));
				repl.Add(Instruction.Create(OpCodes.Ldtoken, dataField));
				repl.Add(Instruction.Create(OpCodes.Call, stubModule.Import(
					typeof(RuntimeHelpers).GetMethod("InitializeArray"))));
				return repl.ToArray();
			});
		}

		void InjectStub(ConfuserContext context, CompressorContext compCtx, ProtectionParameters parameters, ModuleDef stubModule) {
			var rt = context.Registry.GetService<IRuntimeService>();
			RandomGenerator random = context.Registry.GetService<IRandomService>().GetRandomGenerator(Id);
			var comp = context.Registry.GetService<ICompressionService>();

			var rtType = rt.GetRuntimeType(compCtx.CompatMode ? "Confuser.Runtime.CompressorCompat" : "Confuser.Runtime.Compressor");
			IEnumerable<IDnlibDef> defs = InjectHelper.Inject(rtType, stubModule.GlobalType, stubModule);

			switch (parameters.GetParameter(context, context.CurrentModule, "key", Mode.Normal)) {
				case Mode.Normal:
					compCtx.Deriver = new NormalDeriver();
					break;
				case Mode.Dynamic:
					compCtx.Deriver = new DynamicDeriver();
					break;
				default:
					throw new UnreachableException();
			}
			compCtx.Deriver.Init(context, random);

			context.Logger.Debug("Encrypting modules...");

			// Main
			MethodDef entryPoint = defs.OfType<MethodDef>().Single(method => method.Name == "Main");
			stubModule.EntryPoint = entryPoint;

			if (compCtx.EntryPoint.HasAttribute("System.STAThreadAttribute")) {
				var attrType = stubModule.CorLibTypes.GetTypeRef("System", "STAThreadAttribute");
				var ctorSig = MethodSig.CreateInstance(stubModule.CorLibTypes.Void);
				entryPoint.CustomAttributes.Add(new CustomAttribute(
					new MemberRefUser(stubModule, ".ctor", ctorSig, attrType)));
			}
			else if (compCtx.EntryPoint.HasAttribute("System.MTAThreadAttribute")) {
				var attrType = stubModule.CorLibTypes.GetTypeRef("System", "MTAThreadAttribute");
				var ctorSig = MethodSig.CreateInstance(stubModule.CorLibTypes.Void);
				entryPoint.CustomAttributes.Add(new CustomAttribute(
					new MemberRefUser(stubModule, ".ctor", ctorSig, attrType)));
			}

			uint seed = random.NextUInt32();
			compCtx.OriginModule = context.OutputModules[compCtx.ModuleIndex];

			byte[] encryptedModule = compCtx.Encrypt(comp, compCtx.OriginModule, seed,
			                                         progress => context.Logger.Progress((int)(progress * 10000), 10000));
			context.Logger.EndProgress();
			context.CheckCancellation();

			compCtx.EncryptedModule = encryptedModule;

			MutationHelper.InjectKeys(entryPoint,
			                          new[] { 0, 1 },
			                          new[] { encryptedModule.Length >> 2, (int)seed });
			InjectData(stubModule, entryPoint, encryptedModule);

			// Decrypt
			MethodDef decrypter = defs.OfType<MethodDef>().Single(method => method.Name == "Decrypt");
			decrypter.Body.SimplifyMacros(decrypter.Parameters);
			List<Instruction> instrs = decrypter.Body.Instructions.ToList();
			for (int i = 0; i < instrs.Count; i++) {
				Instruction instr = instrs[i];
				if (instr.OpCode == OpCodes.Call) {
					var method = (IMethod)instr.Operand;
					if (method.DeclaringType.Name == "Mutation" &&
					    method.Name == "Crypt") {
						Instruction ldDst = instrs[i - 2];
						Instruction ldSrc = instrs[i - 1];
						Debug.Assert(ldDst.OpCode == OpCodes.Ldloc && ldSrc.OpCode == OpCodes.Ldloc);
						instrs.RemoveAt(i);
						instrs.RemoveAt(i - 1);
						instrs.RemoveAt(i - 2);
						instrs.InsertRange(i - 2, compCtx.Deriver.EmitDerivation(decrypter, context, (Local)ldDst.Operand, (Local)ldSrc.Operand));
					}
					else if (method.DeclaringType.Name == "Lzma" &&
					         method.Name == "Decompress") {
						MethodDef decomp = comp.GetRuntimeDecompressor(stubModule, member => { });
						instr.Operand = decomp;
					}
				}
			}
			decrypter.Body.Instructions.Clear();
			foreach (Instruction instr in instrs)
				decrypter.Body.Instructions.Add(instr);

			// Pack modules
			PackModules(context, compCtx, stubModule, comp, random);
		}

		void ImportAssemblyTypeReferences(ModuleDef originModule, ModuleDef stubModule) {
			var assembly = stubModule.Assembly;
			foreach (var ca in assembly.CustomAttributes) {
				if (ca.AttributeType.Scope == originModule)
					ca.Constructor = (ICustomAttributeType)stubModule.Import(ca.Constructor);
			}
			foreach (var ca in assembly.DeclSecurities.SelectMany(declSec => declSec.CustomAttributes)) {
				if (ca.AttributeType.Scope == originModule)
					ca.Constructor = (ICustomAttributeType)stubModule.Import(ca.Constructor);
			}
		}

		class KeyInjector : IModuleWriterListener {
			readonly CompressorContext ctx;

			public KeyInjector(CompressorContext ctx) {
				this.ctx = ctx;
			}

			public void OnWriterEvent(ModuleWriterBase writer, ModuleWriterEvent evt) {
				if (evt == ModuleWriterEvent.MDBeginCreateTables) {
					// Add key signature
					uint sigBlob = writer.MetaData.BlobHeap.Add(ctx.KeySig);
					uint sigRid = writer.MetaData.TablesHeap.StandAloneSigTable.Add(new RawStandAloneSigRow(sigBlob));
					Debug.Assert(sigRid == 1);
					uint sigToken = 0x11000000 | sigRid;
					ctx.KeyToken = sigToken;
					MutationHelper.InjectKey(writer.Module.EntryPoint, 2, (int)sigToken);
				}
				else if (evt == ModuleWriterEvent.MDBeginAddResources && !ctx.CompatMode) {
					// Compute hash
					byte[] hash = SHA1.Create().ComputeHash(ctx.OriginModule);
					uint hashBlob = writer.MetaData.BlobHeap.Add(hash);

					MDTable<RawFileRow> fileTbl = writer.MetaData.TablesHeap.FileTable;
					uint fileRid = fileTbl.Add(new RawFileRow(
						                           (uint)FileAttributes.ContainsMetaData,
						                           writer.MetaData.StringsHeap.Add("koi"),
						                           hashBlob));
					uint impl = CodedToken.Implementation.Encode(new MDToken(Table.File, fileRid));

					// Add resources
					MDTable<RawManifestResourceRow> resTbl = writer.MetaData.TablesHeap.ManifestResourceTable;
					foreach (var resource in ctx.ManifestResources)
						resTbl.Add(new RawManifestResourceRow(resource.Item1, resource.Item2, writer.MetaData.StringsHeap.Add(resource.Item3), impl));

					// Add exported types
					var exTbl = writer.MetaData.TablesHeap.ExportedTypeTable;
					foreach (var type in ctx.OriginModuleDef.GetTypes()) {
						if (!type.IsVisibleOutside())
							continue;
						exTbl.Add(new RawExportedTypeRow((uint)type.Attributes, 0,
						                                 writer.MetaData.StringsHeap.Add(type.Name),
						                                 writer.MetaData.StringsHeap.Add(type.Namespace), impl));
					}
				}
			}
		}
	}
}