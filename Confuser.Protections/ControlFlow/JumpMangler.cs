using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Confuser.Core.Services;

namespace Confuser.Protections.ControlFlow
{
    class JumpMangler : ManglerBase
    {
        LinkedList<Instruction[]> SpiltFragments(InstrBlock block, CFContext ctx)
        {
            LinkedList<Instruction[]> fragments = new LinkedList<Instruction[]>();
            List<Instruction> currentFragment = new List<Instruction>();

            int skipCount = -1;
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                if (skipCount != -1)
                {
                    if (skipCount > 0)
                    {
                        currentFragment.Add(block.Instructions[i]);
                        skipCount--;
                        continue;
                    }
                    else
                    {
                        fragments.AddLast(currentFragment.ToArray());
                        currentFragment.Clear();

                        skipCount = -1;
                    }
                }

                if (block.Instructions[i].OpCode.OpCodeType == OpCodeType.Prefix)
                {
                    skipCount = 1;
                    currentFragment.Add(block.Instructions[i]);
                    continue;
                }
                else if (i + 2 < block.Instructions.Count &&
                    block.Instructions[i + 0].OpCode.Code == Code.Dup &&
                    block.Instructions[i + 1].OpCode.Code == Code.Ldvirtftn &&
                    block.Instructions[i + 2].OpCode.Code == Code.Newobj)
                {
                    skipCount = 2;
                    currentFragment.Add(block.Instructions[i]);
                    continue;
                }
                else if (i + 1 < block.Instructions.Count &&
                    block.Instructions[i + 0].OpCode.Code == Code.Ldftn &&
                    block.Instructions[i + 1].OpCode.Code == Code.Newobj)
                {
                    skipCount = 1;
                    currentFragment.Add(block.Instructions[i]);
                    continue;
                }
                else
                {
                    currentFragment.Add(block.Instructions[i]);

                    if (ctx.Intensity > ctx.Random.NextDouble())
                    {
                        fragments.AddLast(currentFragment.ToArray());
                        currentFragment.Clear();
                    }
                }
            }

            if (currentFragment.Count > 0)
                fragments.AddLast(currentFragment.ToArray());

            return fragments;
        }

        public override void Mangle(CilBody body, ScopeBlock root, CFContext ctx)
        {
            body.MaxStack++;
            foreach (var block in GetAllBlocks(root))
            {
                var fragments = SpiltFragments(block, ctx);
                if (fragments.Count < 4) continue;

                var current = fragments.First;
                while (current.Next != null)
                {
                    List<Instruction> newFragment = new List<Instruction>(current.Value);
                    ctx.AddJump(newFragment, current.Next.Value[0]);
                    ctx.AddJunk(newFragment);
                    current.Value = newFragment.ToArray();
                    current = current.Next;
                }
                Instruction[] first = fragments.First.Value;
                fragments.RemoveFirst();
                Instruction[] last = fragments.Last.Value;
                fragments.RemoveLast();

                var newFragments = fragments.ToList();
                ctx.Random.Shuffle(newFragments);

                block.Instructions = first
                    .Concat(newFragments.SelectMany(fragment => fragment))
                    .Concat(last).ToList();
            }
        }
    }
}
