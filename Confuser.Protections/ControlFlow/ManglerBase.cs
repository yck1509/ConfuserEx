using System;
using System.Collections.Generic;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal abstract class ManglerBase {
		protected static IEnumerable<InstrBlock> GetAllBlocks(ScopeBlock scope) {
			foreach (BlockBase child in scope.Children) {
				if (child is InstrBlock)
					yield return (InstrBlock)child;
				else {
					foreach (InstrBlock block in GetAllBlocks((ScopeBlock)child))
						yield return block;
				}
			}
		}

		public abstract void Mangle(CilBody body, ScopeBlock root, CFContext ctx);
	}
}