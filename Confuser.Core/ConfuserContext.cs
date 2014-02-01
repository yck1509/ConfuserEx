using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using System.Threading;

namespace Confuser.Core
{
    /// <summary>
    /// Context providing information on the current protection process.
    /// </summary>
    public class ConfuserContext
    {
        /// <summary>
        /// Gets the logger used for logging events.
        /// </summary>
        /// <value>The logger.</value>
        public ILogger Logger { get; internal set; }


        internal CancellationToken token;
        /// <summary>
        /// Throws a System.OperationCanceledException if protection process has been canceled.
        /// </summary>
        public void CheckCancellation()
        {
        }

        /// <summary>
        /// Gets the modules being protected.
        /// </summary>
        /// <value>The modules being protected.</value>
        public IList<ModuleDef> Modules { get; internal set; }

        /// <summary>
        /// Gets the <c>byte[]</c> of modules after protected, or null if module not protected yet.
        /// </summary>
        /// <value>The list of <c>byte[]</c> of protected modules.</value>
        public IList<byte[]> OutputModules { get; internal set; }

        /// <summary>
        /// Gets the relative output paths of module, or null if module not protected yet.
        /// </summary>
        /// <value>The relative output paths of protected modules.</value>
        public IList<string> OutputPath { get; internal set; }

        /// <summary>
        /// Gets the current module in process of protection.
        /// </summary>
        /// <value>The current module.</value>
        public ModuleDef CurrentModule { get; internal set; }

        /// <summary>
        /// Gets the writer options of the current module.
        /// </summary>
        /// <value>The writer options.</value>
        public ModuleWriterOptionsBase CurrentModuleWriterOptions { get; internal set; }

        /// <summary>
        /// Gets output <c>byte[]</c> of the current module
        /// </summary>
        /// <value>The output <c>byte[]</c>.</value>
        public byte[] CurrentModuleOutput { get; internal set; }
    }
}
