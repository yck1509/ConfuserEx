using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Diagnostics;

namespace Confuser.Core.Services
{
    class TraceService : ITraceService
    {
        ConfuserContext context;
        /// <summary>
        /// Initializes a new instance of the <see cref="TraceService"/> class.
        /// </summary>
        /// <param name="context">The working context.</param>
        public TraceService(ConfuserContext context)
        {
            this.context = context;
        }


        Dictionary<MethodDef, MethodTrace> cache = new Dictionary<MethodDef, MethodTrace>();
        /// <inheritdoc/>
        public MethodTrace Trace(MethodDef method)
        {
            if (method == null)
                throw new ArgumentNullException("method");
            return cache.GetValueOrDefaultLazy(method, m => cache[m] = new MethodTrace(this, m)).Trace();
        }
    }

    /// <summary>
    /// Provides methods to trace stack of method body.
    /// </summary>
    public interface ITraceService
    {
        /// <summary>
        /// Trace the stack of the specified method.
        /// </summary>
        /// <param name="method">The method to trace.</param>
        /// <exception cref="InvalidMethodException"><paramref name="method"/> has invalid body.</exception>
        /// <exception cref="System.ArgumentNullException"><paramref name="method"/> is <c>null</c>.</exception>
        MethodTrace Trace(MethodDef method);
    }


    /// <summary>
    /// The trace result of a method.
    /// </summary>
    public class MethodTrace
    {
        TraceService service;
        MethodDef method;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodTrace"/> class.
        /// </summary>
        /// <param name="service">The trace service.</param>
        /// <param name="method">The method to trace.</param>
        internal MethodTrace(TraceService service, MethodDef method)
        {
            this.service = service;
            this.method = method;
        }

        /// <summary>
        /// Gets the method this trace belongs to.
        /// </summary>
        /// <value>The method.</value>
        public MethodDef Method { get { return method; } }

        Dictionary<uint, int> offset2index;
        /// <summary>
        /// Gets the map of offset to index.
        /// </summary>
        /// <value>The map.</value>
        public Func<uint, int> OffsetToIndexMap { get { return offset => offset2index[offset]; } }

        /// <summary>
        /// Gets the stack depths of method body.
        /// </summary>
        /// <value>The stack depths.</value>
        public int[] StackDepths { get; private set; }

        Dictionary<int, List<Instruction>> fromInstrs;

        /// <summary>
        /// Perform the actual tracing.
        /// </summary>
        /// <returns>This instance.</returns>
        /// <exception cref="InvalidMethodException">Bad method body.</exception>
        internal MethodTrace Trace()
        {
            var body = method.Body;
            method.Body.UpdateInstructionOffsets();

            offset2index = new Dictionary<uint, int>();
            var depths = new int[body.Instructions.Count];
            fromInstrs = new Dictionary<int, List<Instruction>>();

            var instrs = body.Instructions;
            for (int i = 0; i < instrs.Count; i++)
            {
                offset2index.Add(instrs[i].Offset, i);
                depths[i] = int.MinValue;
            }

            foreach (var eh in body.ExceptionHandlers)
            {
                depths[offset2index[eh.TryStart.Offset]] = 0;
                depths[offset2index[eh.HandlerStart.Offset]] = (eh.HandlerType != ExceptionHandlerType.Finally ? 1 : 0);
                if (eh.FilterStart != null)
                    depths[offset2index[eh.FilterStart.Offset]] = 1;
            }

            // Just do a simple forward scan to build the stack depth map
            int currentStack = 0;
            for (int i = 0; i < instrs.Count; i++)
            {
                var instr = instrs[i];

                if (depths[i] != int.MinValue)   // Already set due to being target of a branch / beginning of EHs.
                    currentStack = depths[i];

                switch (instr.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                        depths[offset2index[((Instruction)instr.Operand).Offset]] = currentStack;
                        fromInstrs.AddListEntry(offset2index[((Instruction)instr.Operand).Offset], instr);
                        currentStack = 0;
                        break;
                    case FlowControl.Break:
                        break;
                    case FlowControl.Call:
                        if (instr.OpCode.Code == Code.Jmp)
                            currentStack = 0;
                        break;
                    case FlowControl.Cond_Branch:
                        if (instr.OpCode.Code == Code.Switch)
                        {
                            foreach (var target in (Instruction[])instr.Operand)
                            {
                                depths[offset2index[target.Offset]] = currentStack;
                                fromInstrs.AddListEntry(offset2index[target.Offset], instr);
                            }
                        }
                        else
                        {
                            depths[offset2index[((Instruction)instr.Operand).Offset]] = currentStack;
                            fromInstrs.AddListEntry(offset2index[((Instruction)instr.Operand).Offset], instr);
                        }
                        break;
                    case FlowControl.Meta:
                        break;
                    case FlowControl.Next:
                        break;
                    case FlowControl.Return:
                        currentStack = 0;
                        break;
                    case FlowControl.Throw:
                        break;
                    default:
                        throw new UnreachableException();
                }

                instr.UpdateStack(ref currentStack);
                depths[i] = currentStack;
            }
            foreach (var stackDepth in depths)
                if (stackDepth == int.MinValue)
                    throw new InvalidMethodException("Bad method body.");

            StackDepths = depths;

            return this;
        }

        /// <summary>
        /// Traces the arguments of the specified call instruction.
        /// </summary>
        /// <param name="instr">The call instruction.</param>
        /// <returns>The indexes of the begin instruction of arguments.</returns>
        /// <exception cref="System.ArgumentException">The specified call instruction is invalid.</exception>
        /// <exception cref="InvalidMethodException">The method body is invalid.</exception>
        public int[] TraceArguments(Instruction instr)
        {
            if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt)
                throw new ArgumentException("Invalid call instruction.", "instr");

            int push, pop;
            instr.CalculateStackUsage(out push, out pop);   // pop is number of arguments
            int instrIndex = offset2index[instr.Offset];
            int argCount = pop;
            int targetStack = StackDepths[instrIndex - 1] - argCount + 1;

            // Find the begin instruction of method call
            int beginInstrIndex = -1;
            HashSet<uint> seen = new HashSet<uint>();
            Queue<int> working = new Queue<int>();
            working.Enqueue(offset2index[instr.Offset] - 1);
            while (working.Count > 0)
            {
                int index = working.Dequeue();
                while (index >= 0)
                {
                    if (StackDepths[index] == targetStack)
                        break;

                    if (fromInstrs.ContainsKey(index))
                        foreach (var fromInstr in fromInstrs[index])
                        {
                            if (!seen.Contains(fromInstr.Offset))
                            {
                                seen.Add(fromInstr.Offset);
                                working.Enqueue(offset2index[fromInstr.Offset]);
                            }
                        }
                    index--;
                }
                if (index < 0)
                    throw new InvalidMethodException("Empty evaluation stack.");

                if (beginInstrIndex == -1)
                    beginInstrIndex = index;
                else if (beginInstrIndex != index)
                    throw new InvalidMethodException("Stack depth not matched.");
            }

            // Trace the index of arguments
            seen.Clear();
            Queue<Tuple<int, Stack<int>>> working2 = new Queue<Tuple<int, Stack<int>>>();
            working2.Clear();
            working2.Enqueue(Tuple.Create(beginInstrIndex, new Stack<int>()));
            int[] ret = null;
            while (working2.Count > 0)
            {
                var tuple = working2.Dequeue();
                int index = tuple.Item1;
                Stack<int> evalStack = tuple.Item2;

                while (index != instrIndex)
                {
                    Instruction currentInstr = method.Body.Instructions[index];
                    currentInstr.CalculateStackUsage(out push, out pop);
                    int stackUsage = pop - push;
                    if (stackUsage < 0)
                    {
                        Debug.Assert(stackUsage == -1);     // i.e. push
                        evalStack.Push(index);
                    }
                    else
                    {
                        for (int i = 0; i < stackUsage; i++)
                            evalStack.Pop();
                    }

                    object instrOperand = currentInstr.Operand;
                    if (currentInstr.Operand is Instruction)
                    {
                        var targetIndex = offset2index[((Instruction)currentInstr.Operand).Offset];
                        if (currentInstr.OpCode.FlowControl == FlowControl.Branch)
                            index = targetIndex;
                        else
                        {
                            working2.Enqueue(Tuple.Create(targetIndex, new Stack<int>(evalStack)));
                            index++;
                        }
                    }

                    else if (currentInstr.Operand is Instruction[])
                    {
                        foreach (var targetInstr in (Instruction[])currentInstr.Operand)
                            working2.Enqueue(Tuple.Create(offset2index[targetInstr.Offset], new Stack<int>(evalStack)));
                        index++;
                    }
                    else
                        index++;
                }

                if (evalStack.Count != argCount)
                    throw new InvalidMethodException("Cannot find argument index.");
                else if (ret != null && !evalStack.SequenceEqual(ret))
                    throw new InvalidMethodException("Stack depths mismatched.");
                else
                    ret = evalStack.ToArray();
            }

            Array.Reverse(ret);
            if (ret == null)
                throw new InvalidMethodException("Cannot find argument index.");

            return ret;
        }
    }
}
