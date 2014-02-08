using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

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
        /// <exception cref="InvalidMethodException"><paramref name="member"/> has invalid body.</exception>
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

        Dictionary<Instruction, List<Instruction>> fromInstrs;

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
            fromInstrs = new Dictionary<Instruction, List<Instruction>>();

            var instrs = body.Instructions;
            for (int i = 0; i < instrs.Count; i++)
            {
                offset2index.Add(instrs[i].Offset, i);
                depths[i] = -1;
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

                if (depths[i] != -1)   // already set due to being target of a branch.
                    currentStack = depths[i];
                else
                    depths[i] = currentStack;

                instr.UpdateStack(ref currentStack);

                switch (instr.OpCode.FlowControl)
                {
                    case FlowControl.Branch:
                        depths[offset2index[((Instruction)instr.Operand).Offset]] = currentStack;
                        fromInstrs.AddListEntry((Instruction)instr.Operand, instr);
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
                                fromInstrs.AddListEntry(target, instr);
                            }
                        }
                        else
                        {
                            depths[offset2index[((Instruction)instr.Operand).Offset]] = currentStack;
                            fromInstrs.AddListEntry((Instruction)instr.Operand, instr);
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
                        currentStack = 0;
                        break;
                    default:
                        throw new UnreachableException();
                }
            }
            foreach (var stackDepth in depths)
                if (stackDepth == -1)
                    throw new InvalidMethodException("Bad method body.");

            StackDepths = depths;

            return this;
        }
    }
}
