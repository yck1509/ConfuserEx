using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// Base class of Confuser packers.
    /// </summary>
    /// <remarks>
    /// A parameterless constructor must exists in derived classes to enable plugin discovery.
    /// </remarks>
    public abstract class Packer : ConfuserComponent
    {
        /// <summary>
        /// Executes the packer.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="parameters">The parameters of packer.</param>
        protected internal abstract void Pack(ConfuserContext context, ProtectionParameters parameters);
    }
}
