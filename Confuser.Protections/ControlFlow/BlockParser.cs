using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Emit;
using Confuser.Core;
using System.Diagnostics;

namespace Confuser.Protections.ControlFlow
{
    static class BlockParser
    {
        public static ScopeBlock ParseBody(CilBody body)
        {
            var ehScopes = new Dictionary<ExceptionHandler, Tuple<ScopeBlock, ScopeBlock, ScopeBlock>>();
            foreach (var eh in body.ExceptionHandlers)
            {
                ScopeBlock tryBlock = new ScopeBlock(BlockType.Try, eh);

                BlockType handlerType = BlockType.Handler;

                if (eh.HandlerType == ExceptionHandlerType.Finally)
                    handlerType = BlockType.Finally;
                else if (eh.HandlerType == ExceptionHandlerType.Fault)
                    handlerType = BlockType.Fault;

                ScopeBlock handlerBlock = new ScopeBlock(handlerType, eh);

                if (eh.FilterStart != null)
                {
                    ScopeBlock filterBlock = new ScopeBlock(BlockType.Filter, eh);
                    ehScopes[eh] = Tuple.Create(tryBlock, handlerBlock, filterBlock);
                }
                else
                    ehScopes[eh] = Tuple.Create(tryBlock, handlerBlock, (ScopeBlock)null);
            }

            ScopeBlock root = new ScopeBlock(BlockType.Normal, null);
            Stack<ScopeBlock> scopeStack = new Stack<ScopeBlock>();

            scopeStack.Push(root);
            foreach (var instr in body.Instructions)
            {
                foreach (var eh in body.ExceptionHandlers)
                {
                    var ehScope = ehScopes[eh];

                    if (instr == eh.TryEnd)
                        scopeStack.Pop();

                    if (instr == eh.HandlerEnd)
                        scopeStack.Pop();

                    if (eh.FilterStart != null && instr == eh.HandlerStart)
                    {
                        // Filter must precede handler immediately
                        Debug.Assert(scopeStack.Peek().Type == BlockType.Filter);
                        scopeStack.Pop();
                    }
                }
                foreach (var eh in body.ExceptionHandlers.Reverse())
                {
                    var ehScope = ehScopes[eh];
                    var parent = scopeStack.Count > 0 ? scopeStack.Peek() : null;

                    if (instr == eh.TryStart)
                    {
                        if (parent != null)
                            parent.Children.Add(ehScope.Item1);
                        scopeStack.Push(ehScope.Item1);
                    }

                    if (instr == eh.HandlerStart)
                    {
                        if (parent != null)
                            parent.Children.Add(ehScope.Item2);
                        scopeStack.Push(ehScope.Item2);
                    }

                    if (instr == eh.FilterStart)
                    {
                        if (parent != null)
                            parent.Children.Add(ehScope.Item3);
                        scopeStack.Push(ehScope.Item3);
                    }
                }

                ScopeBlock scope = scopeStack.Peek();
                InstrBlock block = scope.Children.LastOrDefault() as InstrBlock;
                if (block == null)
                    scope.Children.Add(block = new InstrBlock());
                block.Instructions.Add(instr);
            }
            Debug.Assert(scopeStack.Count == 1);
            return root;
        }
    }
}
