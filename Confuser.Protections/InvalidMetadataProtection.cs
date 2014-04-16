using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using dnlib.DotNet.MD;
using Confuser.Core.Services;
using System.IO;

namespace Confuser.Protections
{
	class InvalidMetadataProtection : Protection
	{
		public const string _Id = "invalid metadata";
		public const string _FullId = "Ki.InvalidMD";

		protected override void Initialize(ConfuserContext context)
		{
			//
		}

		protected override void PopulatePipeline(ProtectionPipeline pipeline)
		{
			pipeline.InsertPostStage(PipelineStage.BeginModule, new InvalidMDPhase(this));
		}

		public override string Name
		{
			get { return "Invalid Metadata Protection"; }
		}

		public override string Description
		{
			get { return "This protection adds invalid metadata to modules to prevent disassembler/decompiler from opening them."; }
		}

		public override string Id
		{
			get { return _Id; }
		}

		public override string FullId
		{
			get { return _FullId; }
		}

		public override ProtectionPreset Preset
		{
			get { return ProtectionPreset.Maximum; }
		}

		class RawHeap : HeapBase
		{
			string name;
			byte[] content;
			public RawHeap(string name, byte[] content)
			{
				this.name = name;
				this.content = content;
			}

			public override string Name { get { return name; } }

			public override uint GetRawLength() { return (uint)content.Length; }

			protected override void WriteToImpl(BinaryWriter writer)
			{
				writer.Write(content);
			}
		}

		class InvalidMDPhase : ProtectionPhase
		{
			public InvalidMDPhase(InvalidMetadataProtection parent)
				: base(parent)
			{
			}

			public override ProtectionTargets Targets
			{
				get { return ProtectionTargets.Modules; }
			}

			RandomGenerator random;
			protected override void Execute(ConfuserContext context, ProtectionParameters parameters)
			{
				if (parameters.Targets.Contains(context.CurrentModule))
				{
					random = context.Registry.GetService<IRandomService>().GetRandomGenerator(_FullId);
					context.CurrentModuleWriterListener.OnWriterEvent += OnWriterEvent;
				}
			}

			void Randomize<T>(MDTable<T> table) where T : IRawRow
			{
				var rows = table.ToList();
				random.Shuffle(rows);
				table.Reset();
				foreach (var row in rows)
					table.Add(row);
			}

			void OnWriterEvent(object sender, ModuleWriterListenerEventArgs e)
			{
				ModuleWriter writer = (ModuleWriter)sender;
				if (e.WriterEvent == ModuleWriterEvent.MDEndCreateTables)
				{
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

					writer.Options.MetaDataOptions.TablesHeapOptions.ExtraData = random.NextUInt32();
					writer.Options.MetaDataOptions.TablesHeapOptions.UseENC = false;
					writer.Options.MetaDataOptions.MetaDataHeaderOptions.VersionString += "\0\0\0\0";

					writer.Options.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#Strings", new byte[1]));
					writer.Options.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#Blob", new byte[1]));
					writer.Options.MetaDataOptions.OtherHeapsEnd.Add(new RawHeap("#Schema", new byte[1]));

				}
				else if (e.WriterEvent == ModuleWriterEvent.MDOnAllTablesSorted)
				{
					writer.MetaData.TablesHeap.DeclSecurityTable.Add(new RawDeclSecurityRow(
						unchecked((short)0x7fff), 0xffff7fff, 0xffff7fff));
					/*
					writer.MetaData.TablesHeap.ManifestResourceTable.Add(new RawManifestResourceRow(
						0x7fff7fff, (uint)ManifestResourceAttributes.Private, 0x7fff7fff, 2));
					*/
				}
				else if (e.WriterEvent == ModuleWriterEvent.BeginCalculateRvasAndFileOffsets)
				{
					foreach (var section in writer.Sections)
						section.Name = "";
				}
				else if (e.WriterEvent == ModuleWriterEvent.ChunksAddedToSections)
				{
					var newSection = new PESection("", 0xE0000040);
					writer.Sections.Insert(0, newSection);

					uint alignment;

					alignment = writer.TextSection.Remove(writer.MetaData).Value;
					writer.TextSection.Add(writer.MetaData, alignment);

					alignment = writer.TextSection.Remove(writer.NetResources).Value;
					writer.TextSection.Add(writer.NetResources, alignment);

					alignment = writer.TextSection.Remove(writer.MethodBodies).Value;
					newSection.Add(writer.MethodBodies, alignment);

					alignment = writer.TextSection.Remove(writer.Constants).Value;
					newSection.Add(writer.Constants, alignment);
				}
			}
		}
	}
}
