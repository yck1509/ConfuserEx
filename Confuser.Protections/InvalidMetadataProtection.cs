using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using dnlib.DotNet.MD;
using dnlib.DotNet.Writer;

namespace Confuser.Protections {
	internal class InvalidMetadataProtection : Protection {
		public const string _Id = "invalid metadata";
		public const string _FullId = "Ki.InvalidMD";

		public override string Name {
			get { return "Invalid Metadata Protection"; }
		}

		public override string Description {
			get { return "This protection adds invalid metadata to modules to prevent disassembler/decompiler from opening them."; }
		}

		public override string Id {
			get { return _Id; }
		}

		public override string FullId {
			get { return _FullId; }
		}

		public override ProtectionPreset Preset {
			get { return ProtectionPreset.None; }
		}

		protected override void Initialize(ConfuserContext context) {
			//
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.BeginModule, new InvalidMDPhase(this));
		}

		class InvalidMDPhase : ProtectionPhase {
			RandomGenerator random;

			public InvalidMDPhase(InvalidMetadataProtection parent)
				: base(parent) { }

			public override ProtectionTargets Targets {
				get { return ProtectionTargets.Modules; }
			}

			public override string Name {
				get { return "Invalid metadata addition"; }
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				if (parameters.Targets.Contains(context.CurrentModule)) {
					random = context.Registry.GetService<IRandomService>().GetRandomGenerator(_FullId);
					context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
				}
			}

			void Randomize<T>(MDTable<T> table) where T : IRawRow {
				List<T> rows = table.ToList();
				random.Shuffle(rows);
				table.Reset();
				foreach (T row in rows)
					table.Add(row);
			}

			void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e) {
				var writer = (ModuleWriterBase)sender;
				if (e.WriterEvent == ModuleWriterEvent.MDEndCreateTables) {
					// These hurts reflection

					/*
					uint methodLen = (uint)writer.MetaData.TablesHeap.MethodTable.Rows + 1;
					uint fieldLen = (uint)writer.MetaData.TablesHeap.FieldTable.Rows + 1;

					var root = writer.MetaData.TablesHeap.TypeDefTable.Add(new RawTypeDefRow(
							0, 0x7fff7fff, 0, 0x3FFFD, fieldLen, methodLen));
					writer.MetaData.TablesHeap.NestedClassTable.Add(new RawNestedClassRow(root, root));

					var namespaces = writer.MetaData.TablesHeap.TypeDefTable
						.Select(row => row.Namespace)
						.Distinct()
						.ToList();
					foreach (var ns in namespaces)
					{
						if (ns == 0) continue;
						var type = writer.MetaData.TablesHeap.TypeDefTable.Add(new RawTypeDefRow(
							0, 0, ns, 0x3FFFD, fieldLen, methodLen));
						writer.MetaData.TablesHeap.NestedClassTable.Add(new RawNestedClassRow(root, type));
					}
					
					foreach (var row in writer.MetaData.TablesHeap.ParamTable)
						row.Name = 0x7fff7fff;
					*/

					writer.MetaData.TablesHeap.ModuleTable.Add(new RawModuleRow(0, 0x7fff7fff, 0, 0, 0));
					writer.MetaData.TablesHeap.AssemblyTable.Add(new RawAssemblyRow(0, 0, 0, 0, 0, 0, 0, 0x7fff7fff, 0));

					int r = random.NextInt32(8, 16);
					for (int i = 0; i < r; i++)
						writer.MetaData.TablesHeap.ENCLogTable.Add(new RawENCLogRow(random.NextUInt32(), random.NextUInt32()));
					r = random.NextInt32(8, 16);
					for (int i = 0; i < r; i++)
						writer.MetaData.TablesHeap.ENCMapTable.Add(new RawENCMapRow(random.NextUInt32()));

					//Randomize(writer.MetaData.TablesHeap.NestedClassTable);
					Randomize(writer.MetaData.TablesHeap.ManifestResourceTable);
					//Randomize(writer.MetaData.TablesHeap.GenericParamConstraintTable);

					writer.TheOptions.MetaDataOptions.TablesHeapOptions.ExtraData = random.NextUInt32();
					writer.TheOptions.MetaDataOptions.TablesHeapOptions.UseENC = false;
					writer.TheOptions.MetaDataOptions.MetaDataHeaderOptions.VersionString += "\0\0\0\0";

					/*
					We are going to create a new specific '#GUID' Heap to avoid UnConfuserEX to work.
					<sarcasm>UnConfuserEX is so well coded, it relies on static cmp between values</sarcasm>
					If you deobfuscate this tool, you can see that it check for #GUID size and compare it to
					'16', so we have to create a new array of byte wich size is exactly 16 and put it into 
					our brand new Heap
					*/
					//
                    writer.TheOptions.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#GUID", Guid.NewGuid().ToByteArray()));
					//
					writer.TheOptions.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#Strings", new byte[1]));
					writer.TheOptions.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#Blob", new byte[1]));
					writer.TheOptions.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#Schema", new byte[1]));
				}
				else if (e.WriterEvent == ModuleWriterEvent.MDOnAllTablesSorted) {
					writer.MetaData.TablesHeap.DeclSecurityTable.Add(new RawDeclSecurityRow(
						                                                 unchecked(0x7fff), 0xffff7fff, 0xffff7fff));
					/*
					writer.MetaData.TablesHeap.ManifestResourceTable.Add(new RawManifestResourceRow(
						0x7fff7fff, (uint)ManifestResourceAttributes.Private, 0x7fff7fff, 2));
					*/
				}
			}
		}

		class RawHeap : HeapBase {
			readonly byte[] content;
			readonly string name;

			public RawHeap(string name, byte[] content) {
				this.name = name;
				this.content = content;
			}

			public override string Name {
				get { return name; }
			}

			public override uint GetRawLength() {
				return (uint)content.Length;
			}

			protected override void WriteToImpl(BinaryWriter writer) {
				writer.Write(content);
			}
		}
	}
}
