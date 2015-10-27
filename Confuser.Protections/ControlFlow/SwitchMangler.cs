using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections.ControlFlow {
	internal class SwitchMangler : ManglerBase {
		struct Trace {
			public Dictionary<uint, int> RefCount;
			public Dictionary<uint, List<Instruction>> BrRefs;
			public Dictionary<uint, int> BeforeStack;
			public Dictionary<uint, int> AfterStack;

			static void Increment(Dictionary<uint, int> counts, uint key) {
				int value;
				if (!counts.TryGetValue(key, out value))
					value = 0;
				counts[key] = value + 1;
			}

			public Trace(CilBody body, bool hasReturnValue) {
				RefCount = new Dictionary<uint, int>();
				BrRefs = new Dictionary<uint, List<Instruction>>();
				BeforeStack = new Dictionary<uint, int>();
				AfterStack = new Dictionary<uint, int>();

				body.UpdateInstructionOffsets();

				foreach (ExceptionHandler eh in body.ExceptionHandlers) {
					BeforeStack[eh.TryStart.Offset] = 0;
					BeforeStack[eh.HandlerStart.Offset] = (eh.HandlerType != ExceptionHandlerType.Finally ? 1 : 0);
					if (eh.FilterStart != null)
						BeforeStack[eh.FilterStart.Offset] = 1;
				}

				int currentStack = 0;
				for (int i = 0; i < body.Instructions.Count; i++) {
					var instr = body.Instructions[i];

					if (BeforeStack.ContainsKey(instr.Offset))
						currentStack = BeforeStack[instr.Offset];

					BeforeStack[instr.Offset] = currentStack;
					instr.UpdateStack(ref currentStack, hasReturnValue);
					AfterStack[instr.Offset] = currentStack;

					uint offset;
					switch (instr.OpCode.FlowControl) {
						case FlowControl.Branch:
							offset = ((Instruction)instr.Operand).Offset;
							if (!BeforeStack.ContainsKey(offset))
								BeforeStack[offset] = currentStack;

							Increment(RefCount, offset);
							BrRefs.AddListEntry(offset, instr);

							currentStack = 0;
							continue;
						case FlowControl.Call:
							if (instr.OpCode.Code == Code.Jmp)
								currentStack = 0;
							break;
						case FlowControl.Cond_Branch:
							if (instr.OpCode.Code == Code.Switch) {
								foreach (Instruction target in (Instruction[])instr.Operand) {
									if (!BeforeStack.ContainsKey(target.Offset))
										BeforeStack[target.Offset] = currentStack;

									Increment(RefCount, target.Offset);
									BrRefs.AddListEntry(target.Offset, instr);
								}
							}
							else {
								offset = ((Instruction)instr.Operand).Offset;
								if (!BeforeStack.ContainsKey(offset))
									BeforeStack[offset] = currentStack;

								Increment(RefCount, offset);
								BrRefs.AddListEntry(offset, instr);
							}
							break;
						case FlowControl.Meta:
						case FlowControl.Next:
						case FlowControl.Break:
							break;
						case FlowControl.Return:
						case FlowControl.Throw:
							continue;
						default:
							throw new UnreachableException();
					}

					if (i + 1 < body.Instructions.Count) {
						offset = body.Instructions[i + 1].Offset;
						Increment(RefCount, offset);
					}
				}
			}

			public bool IsBranchTarget(uint offset) {
				List<Instruction> src;
				if (BrRefs.TryGetValue(offset, out src))
					return src.Count > 0;
				return false;
			}

			public bool HasMultipleSources(uint offset) {
				int src;
				if (RefCount.TryGetValue(offset, out src))
					return src > 1;
				return false;
			}
		}

		LinkedList<Instruction[]> SpiltStatements(InstrBlock block, Trace trace, CFContext ctx) {
			var statements = new LinkedList<Instruction[]>();
			var currentStatement = new List<Instruction>();

			// Instructions that must be included in the ccurrent statement to ensure all outgoing
			// branches have stack = 0
			var requiredInstr = new HashSet<Instruction>();

			for (int i = 0; i < block.Instructions.Count; i++) {
				Instruction instr = block.Instructions[i];
				currentStatement.Add(instr);

				bool shouldSpilt = i + 1 < block.Instructions.Count && trace.HasMultipleSources(block.Instructions[i + 1].Offset);
				switch (instr.OpCode.FlowControl) {
					case FlowControl.Branch:
					case FlowControl.Cond_Branch:
					case FlowControl.Return:
					case FlowControl.Throw:
						shouldSpilt = true;
						if (trace.AfterStack[instr.Offset] != 0) {
							if (instr.Operand is Instruction)
								requiredInstr.Add((Instruction)instr.Operand);
							else if (instr.Operand is Instruction[]) {
								foreach (var target in (Instruction[])instr.Operand)
									requiredInstr.Add(target);
							}
						}
						break;
				}
				requiredInstr.Remove(instr);
				if ((instr.OpCode.OpCodeType != OpCodeType.Prefix && trace.AfterStack[instr.Offset] == 0 &&
				     requiredInstr.Count == 0) &&
				    (shouldSpilt || ctx.Intensity > ctx.Random.NextDouble())) {
					statements.AddLast(currentStatement.ToArray());
					currentStatement.Clear();
				}
			}

			if (currentStatement.Count > 0)
				statements.AddLast(currentStatement.ToArray());

			return statements;
		}

		static OpCode InverseBranch(OpCode opCode) {
			switch (opCode.Code) {
				case Code.Bge:
					return OpCodes.Blt;
				case Code.Bge_Un:
					return OpCodes.Blt_Un;
				case Code.Blt:
					return OpCodes.Bge;
				case Code.Blt_Un:
					return OpCodes.Bge_Un;
				case Code.Bgt:
					return OpCodes.Ble;
				case Code.Bgt_Un:
					return OpCodes.Ble_Un;
				case Code.Ble:
					return OpCodes.Bgt;
				case Code.Ble_Un:
					return OpCodes.Bgt_Un;
				case Code.Brfalse:
					return OpCodes.Brtrue;
				case Code.Brtrue:
					return OpCodes.Brfalse;
				case Code.Beq:
					return OpCodes.Bne_Un;
				case Code.Bne_Un:
					return OpCodes.Beq;
			}
			throw new NotSupportedException();
		}

		public override void Mangle(CilBody body, ScopeBlock root, CFContext ctx) {
			Trace trace = new Trace(body, ctx.Method.ReturnType.RemoveModifiers().ElementType != ElementType.Void);
			var local = new Local(ctx.Method.Module.CorLibTypes.UInt32);
			body.Variables.Add(local);
			body.InitLocals = true;

			body.MaxStack += 2;
			IPredicate predicate = null;
			if (ctx.Predicate == PredicateType.Normal) {
				predicate = new NormalPredicate(ctx);
			}
			else if (ctx.Predicate == PredicateType.Expression) {
				predicate = new ExpressionPredicate(ctx);
			}
			else if (ctx.Predicate == PredicateType.x86) {
				predicate = new x86Predicate(ctx);
			}

			foreach (InstrBlock block in GetAllBlocks(root)) {
				LinkedList<Instruction[]> statements = SpiltStatements(block, trace, ctx);

				// Make sure .ctor is executed before switch
				if (ctx.Method.IsInstanceConstructor) {
					var newStatement = new List<Instruction>();
					while (statements.First != null) {
						newStatement.AddRange(statements.First.Value);
						Instruction lastInstr = statements.First.Value.Last();
						statements.RemoveFirst();
						if (lastInstr.OpCode == OpCodes.Call && ((IMethod)lastInstr.Operand).Name == ".ctor")
							break;
					}
					statements.AddFirst(newStatement.ToArray());
				}

				if (statements.Count < 3) continue;

				int i;

				var keyId = Enumerable.Range(0, statements.Count).ToArray();
				ctx.Random.Shuffle(keyId);
				var key = new int[keyId.Length];
				for (i = 0; i < key.Length; i++) {
					var q = ctx.Random.NextInt32() & 0x7fffffff;
					key[i] = q - q % statements.Count + keyId[i];
				}

				var statementKeys = new Dictionary<Instruction, int>();
				LinkedListNode<Instruction[]> current = statements.First;
				i = 0;
				while (current != null) {
					if (i != 0)
						statementKeys[current.Value[0]] = key[i];
					i++;
					current = current.Next;
				}

				var statementLast = new HashSet<Instruction>(statements.Select(st => st.Last()));

				Func<IList<Instruction>, bool> hasUnknownSource;
				hasUnknownSource = instrs => instrs.Any(instr => {
					if (trace.HasMultipleSources(instr.Offset))
						return true;
					List<Instruction> srcs;
					if (trace.BrRefs.TryGetValue(instr.Offset, out srcs)) {
						// Target of switch => assume unknown
						if (srcs.Any(src => src.Operand is Instruction[]))
							return true;

						// Not within current instruction block / targeted in first statement
						if (srcs.Any(src => src.Offset <= statements.First.Value.Last().Offset ||
						                    src.Offset >= block.Instructions.Last().Offset))
							return true;

						// Not targeted by the last of statements
						if (srcs.Any(src => statementLast.Contains(src)))
							return true;
					}
					return false;
				});

				var switchInstr = new Instruction(OpCodes.Switch);
				var switchHdr = new List<Instruction>();

				if (predicate != null) {
					predicate.Init(body);
					switchHdr.Add(Instruction.CreateLdcI4(predicate.GetSwitchKey(key[1])));
					predicate.EmitSwitchLoad(switchHdr);
				}
				else {
					switchHdr.Add(Instruction.CreateLdcI4(key[1]));
				}

				switchHdr.Add(Instruction.Create(OpCodes.Dup));
				switchHdr.Add(Instruction.Create(OpCodes.Stloc, local));
				switchHdr.Add(Instruction.Create(OpCodes.Ldc_I4, statements.Count));
				switchHdr.Add(Instruction.Create(OpCodes.Rem_Un));
				switchHdr.Add(switchInstr);

				ctx.AddJump(switchHdr, statements.Last.Value[0]);
				ctx.AddJunk(switchHdr);

				var operands = new Instruction[statements.Count];
				current = statements.First;
				i = 0;
				while (current.Next != null) {
					var newStatement = new List<Instruction>(current.Value);

					if (i != 0) {
						// Convert to switch
						bool converted = false;

						if (newStatement.Last().IsBr()) {
							// Unconditional

							var target = (Instruction)newStatement.Last().Operand;
							int brKey;
							if (!trace.IsBranchTarget(newStatement.Last().Offset) &&
							    statementKeys.TryGetValue(target, out brKey)) {
								var targetKey = predicate != null ? predicate.GetSwitchKey(brKey) : brKey;
								var unkSrc = hasUnknownSource(newStatement);

								newStatement.RemoveAt(newStatement.Count - 1);

								if (unkSrc) {
									newStatement.Add(Instruction.Create(OpCodes.Ldc_I4, targetKey));
								}
								else {
									var thisKey = key[i];
									var r = ctx.Random.NextInt32();
									newStatement.Add(Instruction.Create(OpCodes.Ldloc, local));
									newStatement.Add(Instruction.CreateLdcI4(r));
									newStatement.Add(Instruction.Create(OpCodes.Mul));
									newStatement.Add(Instruction.Create(OpCodes.Ldc_I4, (thisKey * r) ^ targetKey));
									newStatement.Add(Instruction.Create(OpCodes.Xor));
								}

								ctx.AddJump(newStatement, switchHdr[1]);
								ctx.AddJunk(newStatement);
								operands[keyId[i]] = newStatement[0];
								converted = true;
							}
						}
						else if (newStatement.Last().IsConditionalBranch()) {
							// Conditional

							var target = (Instruction)newStatement.Last().Operand;
							int brKey;
							if (!trace.IsBranchTarget(newStatement.Last().Offset) &&
							    statementKeys.TryGetValue(target, out brKey)) {
								bool unkSrc = hasUnknownSource(newStatement);
								int nextKey = key[i + 1];
								OpCode condBr = newStatement.Last().OpCode;
								newStatement.RemoveAt(newStatement.Count - 1);

								if (ctx.Random.NextBoolean()) {
									condBr = InverseBranch(condBr);
									int tmp = brKey;
									brKey = nextKey;
									nextKey = tmp;
								}

								var thisKey = key[i];
								int r = 0, xorKey = 0;
								if (!unkSrc) {
									r = ctx.Random.NextInt32();
									xorKey = thisKey * r;
								}

								Instruction brKeyInstr = Instruction.CreateLdcI4(xorKey ^ (predicate != null ? predicate.GetSwitchKey(brKey) : brKey));
								Instruction nextKeyInstr = Instruction.CreateLdcI4(xorKey ^ (predicate != null ? predicate.GetSwitchKey(nextKey) : nextKey));
								Instruction pop = Instruction.Create(OpCodes.Pop);

								newStatement.Add(Instruction.Create(condBr, brKeyInstr));
								newStatement.Add(nextKeyInstr);
								newStatement.Add(Instruction.Create(OpCodes.Dup));
								newStatement.Add(Instruction.Create(OpCodes.Br, pop));
								newStatement.Add(brKeyInstr);
								newStatement.Add(Instruction.Create(OpCodes.Dup));
								newStatement.Add(pop);

								if (!unkSrc) {
									newStatement.Add(Instruction.Create(OpCodes.Ldloc, local));
									newStatement.Add(Instruction.CreateLdcI4(r));
									newStatement.Add(Instruction.Create(OpCodes.Mul));
									newStatement.Add(Instruction.Create(OpCodes.Xor));
								}

								ctx.AddJump(newStatement, switchHdr[1]);
								ctx.AddJunk(newStatement);
								operands[keyId[i]] = newStatement[0];
								converted = true;
							}
						}

						if (!converted) {
							// Normal

							var targetKey = predicate != null ? predicate.GetSwitchKey(key[i + 1]) : key[i + 1];
							if (!hasUnknownSource(newStatement)) {
								var thisKey = key[i];
								var r = ctx.Random.NextInt32();
								newStatement.Add(Instruction.Create(OpCodes.Ldloc, local));
								newStatement.Add(Instruction.CreateLdcI4(r));
								newStatement.Add(Instruction.Create(OpCodes.Mul));
								newStatement.Add(Instruction.Create(OpCodes.Ldc_I4, (thisKey * r) ^ targetKey));
								newStatement.Add(Instruction.Create(OpCodes.Xor));
							}
							else {
								newStatement.Add(Instruction.Create(OpCodes.Ldc_I4, targetKey));
							}

							ctx.AddJump(newStatement, switchHdr[1]);
							ctx.AddJunk(newStatement);
							operands[keyId[i]] = newStatement[0];
						}
					}
					else
						operands[keyId[i]] = switchHdr[0];

					current.Value = newStatement.ToArray();
					current = current.Next;
					i++;
				}
				operands[keyId[i]] = current.Value[0];
				switchInstr.Operand = operands;

				Instruction[] first = statements.First.Value;
				statements.RemoveFirst();
				Instruction[] last = statements.Last.Value;
				statements.RemoveLast();

				List<Instruction[]> newStatements = statements.ToList();
				ctx.Random.Shuffle(newStatements);

				block.Instructions.Clear();
				block.Instructions.AddRange(first);
				block.Instructions.AddRange(switchHdr);
				foreach (var statement in newStatements)
					block.Instructions.AddRange(statement);
				block.Instructions.AddRange(last);
			}
		}
	}
}