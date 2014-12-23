using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet.Emit;

namespace Confuser.Core.Helpers {
	/// <summary>
	///     A Control Flow Graph (CFG) of a method
	/// </summary>
	public class ControlFlowGraph : IEnumerable<ControlFlowBlock> {
		readonly List<ControlFlowBlock> blocks;
		readonly CilBody body;
		readonly int[] instrBlocks;
		readonly Dictionary<Instruction, int> indexMap;

		ControlFlowGraph(CilBody body) {
			this.body = body;
			instrBlocks = new int[body.Instructions.Count];
			blocks = new List<ControlFlowBlock>();

			indexMap = new Dictionary<Instruction, int>();
			for (int i = 0; i < body.Instructions.Count; i++)
				indexMap.Add(body.Instructions[i], i);
		}

		/// <summary>
		///     Gets the number of blocks in this CFG.
		/// </summary>
		/// <value>The number of blocks.</value>
		public int Count {
			get { return blocks.Count; }
		}

		/// <summary>
		///     Gets the <see cref="ControlFlowBlock" /> of the specified id.
		/// </summary>
		/// <param name="id">The id.</param>
		/// <returns>The block with specified id.</returns>
		public ControlFlowBlock this[int id] {
			get { return blocks[id]; }
		}

		/// <summary>
		///     Gets the corresponding method body.
		/// </summary>
		/// <value>The method body.</value>
		public CilBody Body {
			get { return body; }
		}

		IEnumerator<ControlFlowBlock> IEnumerable<ControlFlowBlock>.GetEnumerator() {
			return blocks.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return blocks.GetEnumerator();
		}

		/// <summary>
		///     Gets the block containing the specified instruction.
		/// </summary>
		/// <param name="instrIndex">The index of instruction.</param>
		/// <returns>The block containing the instruction.</returns>
		public ControlFlowBlock GetContainingBlock(int instrIndex) {
			return blocks[instrBlocks[instrIndex]];
		}

		/// <summary>
		///     Gets the index of the specified instruction.
		/// </summary>
		/// <param name="instr">The instruction.</param>
		/// <returns>The index of instruction.</returns>
		public int IndexOf(Instruction instr) {
			return indexMap[instr];
		}

		void PopulateBlockHeaders(HashSet<Instruction> blockHeaders, HashSet<Instruction> entryHeaders) {
			for (int i = 0; i < body.Instructions.Count; i++) {
				Instruction instr = body.Instructions[i];

				if (instr.Operand is Instruction) {
					blockHeaders.Add((Instruction)instr.Operand);
					if (i + 1 < body.Instructions.Count)
						blockHeaders.Add(body.Instructions[i + 1]);
				}
				else if (instr.Operand is Instruction[]) {
					foreach (Instruction target in (Instruction[])instr.Operand)
						blockHeaders.Add(target);
					if (i + 1 < body.Instructions.Count)
						blockHeaders.Add(body.Instructions[i + 1]);
				}
				else if ((instr.OpCode.FlowControl == FlowControl.Throw || instr.OpCode.FlowControl == FlowControl.Return) &&
				         i + 1 < body.Instructions.Count) {
					blockHeaders.Add(body.Instructions[i + 1]);
				}
			}
			blockHeaders.Add(body.Instructions[0]);
			foreach (ExceptionHandler eh in body.ExceptionHandlers) {
				blockHeaders.Add(eh.TryStart);
				blockHeaders.Add(eh.HandlerStart);
				blockHeaders.Add(eh.FilterStart);
				entryHeaders.Add(eh.HandlerStart);
				entryHeaders.Add(eh.FilterStart);
			}
		}

		void SplitBlocks(HashSet<Instruction> blockHeaders, HashSet<Instruction> entryHeaders) {
			int nextBlockId = 0;
			int currentBlockId = -1;
			Instruction currentBlockHdr = null;

			for (int i = 0; i < body.Instructions.Count; i++) {
				Instruction instr = body.Instructions[i];
				if (blockHeaders.Contains(instr)) {
					if (currentBlockHdr != null) {
						Instruction footer = body.Instructions[i - 1];

						var type = ControlFlowBlockType.Normal;
						if (entryHeaders.Contains(currentBlockHdr) || currentBlockHdr == body.Instructions[0])
							type |= ControlFlowBlockType.Entry;
						if (footer.OpCode.FlowControl == FlowControl.Return || footer.OpCode.FlowControl == FlowControl.Throw)
							type |= ControlFlowBlockType.Exit;

						blocks.Add(new ControlFlowBlock(currentBlockId, type, currentBlockHdr, footer));
					}

					currentBlockId = nextBlockId++;
					currentBlockHdr = instr;
				}

				instrBlocks[i] = currentBlockId;
			}
			if (blocks.Count == 0 || blocks[blocks.Count - 1].Id != currentBlockId) {
				Instruction footer = body.Instructions[body.Instructions.Count - 1];

				var type = ControlFlowBlockType.Normal;
				if (entryHeaders.Contains(currentBlockHdr) || currentBlockHdr == body.Instructions[0])
					type |= ControlFlowBlockType.Entry;
				if (footer.OpCode.FlowControl == FlowControl.Return || footer.OpCode.FlowControl == FlowControl.Throw)
					type |= ControlFlowBlockType.Exit;

				blocks.Add(new ControlFlowBlock(currentBlockId, type, currentBlockHdr, footer));
			}
		}

		void LinkBlocks() {
			for (int i = 0; i < body.Instructions.Count; i++) {
				Instruction instr = body.Instructions[i];
				if (instr.Operand is Instruction) {
					ControlFlowBlock srcBlock = blocks[instrBlocks[i]];
					ControlFlowBlock dstBlock = blocks[instrBlocks[indexMap[(Instruction)instr.Operand]]];
					dstBlock.Sources.Add(srcBlock);
					srcBlock.Targets.Add(dstBlock);
				}
				else if (instr.Operand is Instruction[]) {
					foreach (Instruction target in (Instruction[])instr.Operand) {
						ControlFlowBlock srcBlock = blocks[instrBlocks[i]];
						ControlFlowBlock dstBlock = blocks[instrBlocks[indexMap[target]]];
						dstBlock.Sources.Add(srcBlock);
						srcBlock.Targets.Add(dstBlock);
					}
				}
			}
			for (int i = 0; i < blocks.Count; i++) {
				if (blocks[i].Footer.OpCode.FlowControl != FlowControl.Branch &&
				    blocks[i].Footer.OpCode.FlowControl != FlowControl.Return &&
				    blocks[i].Footer.OpCode.FlowControl != FlowControl.Throw) {
					blocks[i].Targets.Add(blocks[i + 1]);
					blocks[i + 1].Sources.Add(blocks[i]);
				}
			}
		}

		/// <summary>
		///     Constructs a CFG from the specified method body.
		/// </summary>
		/// <param name="body">The method body.</param>
		/// <returns>The CFG of the given method body.</returns>
		public static ControlFlowGraph Construct(CilBody body) {
			var graph = new ControlFlowGraph(body);
			if (body.Instructions.Count == 0)
				return graph;

			// Populate block headers
			var blockHeaders = new HashSet<Instruction>();
			var entryHeaders = new HashSet<Instruction>();
			graph.PopulateBlockHeaders(blockHeaders, entryHeaders);

			// Split blocks
			graph.SplitBlocks(blockHeaders, entryHeaders);

			// Link blocks
			graph.LinkBlocks();

			return graph;
		}
	}

	/// <summary>
	///     The type of Control Flow Block
	/// </summary>
	[Flags]
	public enum ControlFlowBlockType {
		/// <summary>
		///     The block is a normal block
		/// </summary>
		Normal = 0,

		/// <summary>
		///     There are unknown edges to this block. Usually used at exception handlers / method entry.
		/// </summary>
		Entry = 1,

		/// <summary>
		///     There are unknown edges from this block. Usually used at filter blocks / throw / method exit.
		/// </summary>
		Exit = 2
	}

	/// <summary>
	///     A block in Control Flow Graph (CFG).
	/// </summary>
	public class ControlFlowBlock {
		/// <summary>
		///     The footer instruction
		/// </summary>
		public readonly Instruction Footer;

		/// <summary>
		///     The header instruction
		/// </summary>
		public readonly Instruction Header;

		/// <summary>
		///     The identifier of this block
		/// </summary>
		public readonly int Id;

		/// <summary>
		///     The type of this block
		/// </summary>
		public readonly ControlFlowBlockType Type;

		internal ControlFlowBlock(int id, ControlFlowBlockType type, Instruction header, Instruction footer) {
			Id = id;
			Type = type;
			Header = header;
			Footer = footer;

			Sources = new List<ControlFlowBlock>();
			Targets = new List<ControlFlowBlock>();
		}

		/// <summary>
		///     Gets the source blocks of this control flow block.
		/// </summary>
		/// <value>The source blocks.</value>
		public IList<ControlFlowBlock> Sources { get; private set; }

		/// <summary>
		///     Gets the target blocks of this control flow block.
		/// </summary>
		/// <value>The target blocks.</value>
		public IList<ControlFlowBlock> Targets { get; private set; }

		/// <summary>
		///     Returns a <see cref="System.String" /> that represents this block.
		/// </summary>
		/// <returns>A <see cref="System.String" /> that represents this block.</returns>
		public override string ToString() {
			return string.Format("Block {0} => {1} {2}", Id, Type, string.Join(", ", Targets.Select(block => block.Id.ToString()).ToArray()));
		}
	}
}