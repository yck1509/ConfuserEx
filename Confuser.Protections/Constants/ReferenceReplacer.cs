using System;
using System.Collections.Generic;
using System.Diagnostics;
using Confuser.Core;
using Confuser.Core.Helpers;
using Confuser.Core.Services;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.Constants {
	internal class ReferenceReplacer {

		public static void ReplaceReference(CEContext ctx, ProtectionParameters parameters) {
			foreach (var entry in ctx.ReferenceRepl) {
				if (parameters.GetParameter<bool>(ctx.Context, entry.Key, "cfg"))
					ReplaceCFG(entry.Key, entry.Value, ctx);
				else
					ReplaceNormal(entry.Key, entry.Value);
			}
		}

		private static void ReplaceNormal(MethodDef method, List<Tuple<Instruction, uint, IMethod>> instrs) {
			foreach (var instr in instrs) {
				int i = method.Body.Instructions.IndexOf(instr.Item1);
				instr.Item1.OpCode = OpCodes.Ldc_I4;
				instr.Item1.Operand = (int)instr.Item2;
				method.Body.Instructions.Insert(i + 1, Instruction.Create(OpCodes.Call, instr.Item3));
			}
		}

		private struct CFGContext {

			public ControlFlowGraph Graph;
			public BlockKey[] Keys;
			public RandomGenerator Random;
			public Local StateVariable;

		}

		private static void InsertEmptyStateUpdate(CFGContext ctx, ControlFlowBlock block) {
			var body = ctx.Graph.Body;
			var key = ctx.Keys[block.Id];
			if (key.EntryState == key.ExitState)
				return;

			// Cannot use graph.IndexOf because instructions has been modified.
			int targetIndex = body.Instructions.IndexOf(block.Header);

			Instruction first;
			if (key.Type == BlockKeyType.Incremental) {
				body.Instructions.Insert(targetIndex + 0, first = Instruction.Create(OpCodes.Ldloc, ctx.StateVariable));
				switch (ctx.Random.NextInt32(3)) {
					case 0:
						body.Instructions.Insert(targetIndex + 1, Instruction.Create(OpCodes.Ldc_I4, (int)(key.EntryState ^ key.ExitState)));
						body.Instructions.Insert(targetIndex + 2, Instruction.Create(OpCodes.Xor));
						break;
					case 1:
						body.Instructions.Insert(targetIndex + 1, Instruction.Create(OpCodes.Ldc_I4, (int)(key.ExitState - key.EntryState)));
						body.Instructions.Insert(targetIndex + 2, Instruction.Create(OpCodes.Add));
						break;
					case 2:
						body.Instructions.Insert(targetIndex + 1, Instruction.Create(OpCodes.Ldc_I4, (int)(key.EntryState - key.ExitState)));
						body.Instructions.Insert(targetIndex + 2, Instruction.Create(OpCodes.Sub));
						break;
				}
				body.Instructions.Insert(targetIndex + 3, Instruction.Create(OpCodes.Stloc, ctx.StateVariable));
			}
			else {
				body.Instructions.Insert(targetIndex + 0, first = Instruction.Create(OpCodes.Ldc_I4, (int)key.ExitState));
				body.Instructions.Insert(targetIndex + 1, Instruction.Create(OpCodes.Stloc, ctx.StateVariable));
			}

			ctx.Graph.Body.ReplaceReference(block.Header, first);
		}

		private static int InsertStateGetAndUpdate(CFGContext ctx, int index, BlockKeyType type, uint currentState, uint targetState) {
			var body = ctx.Graph.Body;

			if (type == BlockKeyType.Incremental) {
				body.Instructions.Insert(index + 0, Instruction.Create(OpCodes.Ldloc, ctx.StateVariable));
				body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Dup));
				switch (ctx.Random.NextInt32(3)) {
					case 0:
						body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ldc_I4, (int)(currentState ^ targetState)));
						body.Instructions.Insert(index + 3, Instruction.Create(OpCodes.Xor));
						break;
					case 1:
						body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ldc_I4, (int)(targetState - currentState)));
						body.Instructions.Insert(index + 3, Instruction.Create(OpCodes.Add));
						break;
					case 2:
						body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ldc_I4, (int)(currentState - targetState)));
						body.Instructions.Insert(index + 3, Instruction.Create(OpCodes.Sub));
						break;
				}
				body.Instructions.Insert(index + 4, Instruction.Create(OpCodes.Stloc, ctx.StateVariable));
				return index + 5;
			}
			body.Instructions.Insert(index + 0, Instruction.Create(OpCodes.Ldc_I4, (int)currentState));
			body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Ldc_I4, (int)targetState));
			body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Stloc, ctx.StateVariable));
			return index + 3;
		}

		private static void ReplaceCFG(MethodDef method, List<Tuple<Instruction, uint, IMethod>> instrs, CEContext ctx) {
			var graph = ControlFlowGraph.Construct(method.Body);
			var sequence = KeySequence.ComputeKeys(graph, ctx.Random);

			var cfgCtx = new CFGContext {
				Graph = graph,
				Keys = sequence,
				Random = ctx.Random
			};
			var blockReferences = new Dictionary<int, SortedList<int, Tuple<Instruction, uint, IMethod>>>();
			foreach (var instr in instrs) {
				var index = graph.IndexOf(instr.Item1);
				var block = graph.GetContainingBlock(index);

				SortedList<int, Tuple<Instruction, uint, IMethod>> list;
				if (!blockReferences.TryGetValue(block.Id, out list))
					list = blockReferences[block.Id] = new SortedList<int, Tuple<Instruction, uint, IMethod>>();

				list.Add(index, instr);
			}

			cfgCtx.StateVariable = new Local(method.Module.CorLibTypes.UInt32);
			method.Body.Variables.Add(cfgCtx.StateVariable);
			method.Body.InitLocals = true;

			// Update state for blocks not in use
			for (int i = 0; i < graph.Count; i++) {
				if (blockReferences.ContainsKey(i))
					continue;
				InsertEmptyStateUpdate(cfgCtx, graph[i]);
			}

			// Update references
			foreach (var blockRef in blockReferences) {
				var key = sequence[blockRef.Key];
				var currentState = key.EntryState;
				var type = key.Type;

				for (int i = 0; i < blockRef.Value.Count; i++) {
					var entry = blockRef.Value.Values[i];
					var targetState = i == blockRef.Value.Count - 1 ? key.ExitState : cfgCtx.Random.NextUInt32();
					var index = graph.Body.Instructions.IndexOf(entry.Item1);
					var id = entry.Item2 ^ currentState;

					entry.Item1.OpCode = OpCodes.Ldc_I4;
					entry.Item1.Operand = (int)id;
					index = InsertStateGetAndUpdate(cfgCtx, index + 1, type, currentState, targetState);
					method.Body.Instructions.Insert(index + 0, Instruction.Create(OpCodes.Xor));
					method.Body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Call, entry.Item3));

					type = BlockKeyType.Incremental;
					currentState = targetState;
				}
			}
		}

	}
}