using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Core
{
    /// <summary>
    /// Context providing information on the current process to <see cref="Protection"/>s.
    /// </summary>
    public class ProtectionContext
    {
        /// <summary>
        /// Gets the <see cref="ConfuserEngine"/> this context belongs to.
        /// </summary>
        /// <value>The parent <see cref="ConfuserEngine"/>.</value>
        public ConfuserEngine Engine { get; private set; }

        /// <summary>
        /// Gets the modules being protected.
        /// </summary>
        /// <value>The modules being protected.</value>
        public IList<ModuleDef> Modules { get; private set; }

        /// <summary>
        /// Gets the <c>byte[]</c> of modules after protected, or null if module not protected yet.
        /// </summary>
        /// <value>The list of <c>byte[]</c> of protected modules.</value>
        public IList<byte[]> OutputModules { get; private set; }

        /// <summary>
        /// Gets the relative output paths of module, or null if module not protected yet.
        /// </summary>
        /// <value>The relative output paths of protected modules.</value>
        public IList<string> OutputPath { get; private set; }

        /// <summary>
        /// Gets the current module in process of protection.
        /// </summary>
        /// <value>The current module.</value>
        public ModuleDef CurrentModule { get; private set; }

        /// <summary>
        /// Gets the writer options of the current module.
        /// </summary>
        /// <value>The writer options.</value>
        public ModuleWriterOptionsBase CurrentModuleWriterOptions { get; private set; }

        /// <summary>
        /// Gets output <c>byte[]</c> of the current module
        /// </summary>
        /// <value>The output <c>byte[]</c>.</value>
        public byte[] CurrentModuleOutput { get; private set; }
    }
}
