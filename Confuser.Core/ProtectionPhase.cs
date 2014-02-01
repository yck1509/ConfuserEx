using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// Base class of <see cref="Protection"/> phases.
    /// </summary>
    public abstract class ProtectionPhase
    {
        /// <summary>
        /// Gets the parent protection.
        /// </summary>
        /// <value>The parent protection.</value>
        public abstract Protection Parent { get; }

        /// <summary>
        /// Executes the protection phase.
        /// </summary>
        /// <param name="context">The context of protection.</param>
        /// <param name="parameters">The parameters of protection.</param>
        protected internal abstract void Execute(ConfuserContext context, ProtectionParameters parameters);
    }
}
