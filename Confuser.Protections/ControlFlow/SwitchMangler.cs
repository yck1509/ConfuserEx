using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using Confuser.Core.Services;
using dnlib.DotNet;
using Confuser.DynCipher;

namespace Confuser.Protections.ControlFlow
{
    class SwitchMangler : ManglerBase
    {
        LinkedList<Instruction[]> SpiltStatements(InstrBlock block, MethodTrace trace, CFContext ctx)
        {
            LinkedList<Instruction[]> statements = new LinkedList<Instruction[]>();
            List<Instruction> currentStatement = new List<Instruction>();

            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var instr = block.Instructions[i];
                currentStatement.Add(instr);

                bool nextIsBrTarget = i + 1 < block.Instructions.Count && trace.IsBranchTarget(trace.OffsetToIndexMap(block.Instructions[i + 1].Offset));

                if ((instr.OpCode.OpCodeType != OpCodeType.Prefix && trace.StackDepths[trace.OffsetToIndexMap(instr.Offset)] == 0) &&
                    (nextIsBrTarget || ctx.Intensity > ctx.Random.NextDouble()))
                {
                    statements.AddLast(currentStatement.ToArray());
                    currentStatement.Clear();
                }
            }

            if (currentStatement.Count > 0)
                statements.AddLast(currentStatement.ToArray());

            return statements;
        }

        static OpCode InverseBranch(OpCode opCode)
        {
            switch (opCode.Code)
            {
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

        public override void Mangle(CilBody body, ScopeBlock root, CFContext ctx)
        {
            MethodTrace trace = ctx.Context.Registry.GetService<ITraceService>().Trace(ctx.Method);

            body.MaxStack++;
            IPredicate predicate = null;
            if (ctx.Predicate == PredicateType.Expression)
                predicate = new ExpressionPredicate(ctx);
            else if (ctx.Predicate == PredicateType.x86)
                predicate = new x86Predicate(ctx);

            foreach (var block in GetAllBlocks(root))
            {
                var statements = SpiltStatements(block, trace, ctx);

                // Make sure .ctor is executed before switch
                if (ctx.Method.IsInstanceConstructor)
                {
                    List<Instruction> newStatement = new List<Instruction>();
                    while (statements.First != null)
                    {
                        newStatement.AddRange(statements.First.Value);
                        Instruction lastInstr = statements.First.Value.Last();
                        statements.RemoveFirst();
                        if (lastInstr.OpCode == OpCodes.Call && ((IMethod)lastInstr.Operand).Name == ".ctor")
                            break;
                    }
                    statements.AddFirst(newStatement.ToArray());
                }

                if (statements.Count < 3) continue;

                int[] key = Enumerable.Range(0, statements.Count).ToArray();
                ctx.Random.Shuffle(key);

                Dictionary<Instruction, int> statementKeys = new Dictionary<Instruction, int>();
                var current = statements.First;
                int i = 0;
                while (current != null)
                {
                    statementKeys[current.Value[0]] = key[i++];
                    current = current.Next;
                }

                Instruction switchInstr = new Instruction(OpCodes.Switch);

                var switchHdr = new List<Instruction>();

                if (predicate != null)
                {
                    predicate.Init(body);
                    switchHdr.Add(Instruction.CreateLdcI4(predicate.GetSwitchKey(key[1])));
                    predicate.EmitSwitchLoad(switchHdr);
                }
                else
                {
                    switchHdr.Add(Instruction.CreateLdcI4(key[1]));
                }

                switchHdr.Add(switchInstr);

                ctx.AddJump(switchHdr, statements.Last.Value[0]);
                ctx.AddJunk(switchHdr);

                Instruction[] operands = new Instruction[statements.Count];
                current = statements.First;
                i = 0;
                while (current.Next != null)
                {
                    List<Instruction> newStatement = new List<Instruction>(current.Value);

                    if (i != 0)
                    {
                        // Convert to switch
                        bool converted = false;

                        if (newStatement.Last().IsBr())
                        {
                            // Unconditional

                            Instruction target = (Instruction)newStatement.Last().Operand;
                            int brKey;
                            if (!trace.IsBranchTarget(trace.OffsetToIndexMap(newStatement.Last().Offset)) &&
                                statementKeys.TryGetValue(target, out brKey))
                            {
                                newStatement.RemoveAt(newStatement.Count - 1);
                                newStatement.Add(Instruction.CreateLdcI4(predicate != null ? predicate.GetSwitchKey(brKey) : brKey));
                                ctx.AddJump(newStatement, switchHdr[1]);
                                ctx.AddJunk(newStatement);
                                operands[key[i]] = newStatement[0];
                                converted = true;
                            }
                        }

                        else if (newStatement.Last().IsConditionalBranch())
                        {
                            // Conditional

                            Instruction target = (Instruction)newStatement.Last().Operand;
                            int brKey;
                            if (!trace.IsBranchTarget(trace.OffsetToIndexMap(newStatement.Last().Offset)) &&
                                statementKeys.TryGetValue(target, out brKey))
                            {
                                int nextKey = key[i + 1];
                                OpCode condBr = newStatement.Last().OpCode;
                                newStatement.RemoveAt(newStatement.Count - 1);

                                if (ctx.Random.NextBoolean())
                                {
                                    condBr = InverseBranch(condBr);
                                    int tmp = brKey;
                                    brKey = nextKey;
                                    nextKey = tmp;
                                }

                                Instruction brKeyInstr = Instruction.CreateLdcI4(predicate != null ? predicate.GetSwitchKey(brKey) : brKey);
                                Instruction nextKeyInstr = Instruction.CreateLdcI4(predicate != null ? predicate.GetSwitchKey(nextKey) : nextKey);
                                Instruction pop = Instruction.Create(OpCodes.Pop);
                                newStatement.Add(Instruction.Create(condBr, brKeyInstr));
                                newStatement.Add(nextKeyInstr);
                                newStatement.Add(Instruction.Create(OpCodes.Dup));
                                newStatement.Add(Instruction.Create(OpCodes.Br, pop));
                                newStatement.Add(brKeyInstr);
                                newStatement.Add(Instruction.Create(OpCodes.Dup));
                                newStatement.Add(pop);

                                ctx.AddJump(newStatement, switchHdr[1]);
                                ctx.AddJunk(newStatement);
                                operands[key[i]] = newStatement[0];
                                converted = true;
                            }
                        }

                        if (!converted)
                        {
                            // Normal

                            newStatement.Add(Instruction.CreateLdcI4(predicate != null ? predicate.GetSwitchKey(key[i + 1]) : key[i + 1]));
                            ctx.AddJump(newStatement, switchHdr[1]);
                            ctx.AddJunk(newStatement);
                            operands[key[i]] = newStatement[0];
                        }
                    }
                    else
                        operands[key[i]] = switchHdr[0];

                    current.Value = newStatement.ToArray();
                    current = current.Next;
                    i++;
                }
                operands[key[i]] = current.Value[0];
                switchInstr.Operand = operands;

                Instruction[] first = statements.First.Value;
                statements.RemoveFirst();
                Instruction[] last = statements.Last.Value;
                statements.RemoveLast();

                var newStatements = statements.ToList();
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
