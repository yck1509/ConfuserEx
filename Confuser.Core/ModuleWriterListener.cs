using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet.Writer;

namespace Confuser.Core
{
    /// <summary>
    /// The listener of module writer event.
    /// </summary>
    public class ModuleWriterListener : IModuleWriterListener
    {
        /// <summary>
        /// Occurs when a module writer event is triggered.
        /// </summary>
        public event EventHandler<ModuleWriterListenerEventArgs> OnWriterEvent;

        /// <inheritdoc/>
        void IModuleWriterListener.OnWriterEvent(ModuleWriterBase writer, ModuleWriterEvent evt)
        {
            if (OnWriterEvent != null)
            {
                OnWriterEvent(writer, new ModuleWriterListenerEventArgs(evt));
            }
        }
    }

    /// <summary>
    /// Indicates the triggered writer event.
    /// </summary>
    public class ModuleWriterListenerEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleWriterListenerEventArgs"/> class.
        /// </summary>
        /// <param name="evt">The triggered writer event.</param>
        public ModuleWriterListenerEventArgs(ModuleWriterEvent evt)
        {
            this.WriterEvent = evt;
        }

        /// <summary>
        /// Gets the triggered writer event.
        /// </summary>
        /// <value>The triggered writer event.</value>
        public ModuleWriterEvent WriterEvent { get; private set; }
    }
}
