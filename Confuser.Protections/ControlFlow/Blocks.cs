using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal abstract class BlockBase {
		public BlockBase(BlockType type) {
			Type = type;
		}

		public ScopeBlock Parent { get; private set; }

		public BlockType Type { get; private set; }
		public abstract void ToBody(CilBody body);
	}

	internal enum BlockType {
		Normal,
		Try,
		Handler,
		Finally,
		Filter,
		Fault
	}

	internal class ScopeBlock : BlockBase {
		public ScopeBlock(BlockType type, ExceptionHandler handler)
			: base(type) {
			Handler = handler;
			Children = new List<BlockBase>();
		}

		public ExceptionHandler Handler { get; private set; }

		public List<BlockBase> Children { get; set; }

		public override string ToString() {
			var ret = new StringBuilder();
			if (Type == BlockType.Try)
				ret.Append("try ");
			else if (Type == BlockType.Handler)
				ret.Append("handler ");
			else if (Type == BlockType.Finally)
				ret.Append("finally ");
			else if (Type == BlockType.Fault)
				ret.Append("fault ");
			ret.AppendLine("{");
			foreach (BlockBase child in Children)
				ret.Append(child);
			ret.AppendLine("}");
			return ret.ToString();
		}

		public Instruction GetFirstInstr() {
			BlockBase firstBlock = Children.First();
			if (firstBlock is ScopeBlock)
				return ((ScopeBlock)firstBlock).GetFirstInstr();
			return ((InstrBlock)firstBlock).Instructions.First();
		}

		public Instruction GetLastInstr() {
			BlockBase firstBlock = Children.Last();
			if (firstBlock is ScopeBlock)
				return ((ScopeBlock)firstBlock).GetLastInstr();
			return ((InstrBlock)firstBlock).Instructions.Last();
		}

		public override void ToBody(CilBody body) {
			if (Type != BlockType.Normal) {
				if (Type == BlockType.Try) {
					Handler.TryStart = GetFirstInstr();
					Handler.TryEnd = GetLastInstr();
				}
				else if (Type == BlockType.Filter) {
					Handler.FilterStart = GetFirstInstr();
				}
				else {
					Handler.HandlerStart = GetFirstInstr();
					Handler.HandlerEnd = GetLastInstr();
				}
			}

			foreach (BlockBase block in Children)
				block.ToBody(body);
		}
	}

	internal class InstrBlock : BlockBase {
		public InstrBlock()
			: base(BlockType.Normal) {
			Instructions = new List<Instruction>();
		}

		public List<Instruction> Instructions { get; set; }

		public override string ToString() {
			var ret = new StringBuilder();
			foreach (Instruction instr in Instructions)
				ret.AppendLine(instr.ToString());
			return ret.ToString();
		}

		public override void ToBody(CilBody body) {
			foreach (Instruction instr in Instructions)
				body.Instructions.Add(instr);
		}
	}
}