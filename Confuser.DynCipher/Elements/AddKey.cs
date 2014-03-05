using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.DynCipher.AST;
using Confuser.DynCipher.Generation;
using Confuser.Core.Services;

namespace Confuser.DynCipher.Elements
{
    class AddKey : CryptoElement
    {
        public AddKey(int index)
            : base(0)
        {
            this.Index = index;
        }

        public int Index { get; private set; }

        public override void Initialize(RandomGenerator random)
        {
        }

        void EmitCore(CipherGenContext context)
        {
            Expression val = context.GetDataExpression(Index);

            context.Emit(new AssignmentStatement()
            {
                Value = val ^ context.GetKeyExpression(Index),
                Target = val
            });
        }

        public override void Emit(CipherGenContext context)
        {
            EmitCore(context);
        }

        public override void EmitInverse(CipherGenContext context)
        {
            EmitCore(context);
        }
    }
}
