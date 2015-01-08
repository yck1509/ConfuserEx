using System;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Confuser.Core {
	/// <summary>
	///     The listener of module writer event.
	/// </summary>
	public class ModuleWriterListener : IModuleWriterListener {
		/// <inheritdoc />
		void IModuleWriterListener.OnWriterEvent(ModuleWriterBase writer, ModuleWriterEvent evt) {
			if (evt == ModuleWriterEvent.PESectionsCreated)
				NativeEraser.Erase(writer as NativeModuleWriter, writer.Module as ModuleDefMD);
			if (OnWriterEvent != null) {
				OnWriterEvent(writer, new ModuleWriterListenerEventArgs(evt));
			}
		}

		/// <summary>
		///     Occurs when a module writer event is triggered.
		/// </summary>
		public event EventHandler<ModuleWriterListenerEventArgs> OnWriterEvent;
	}

	/// <summary>
	///     Indicates the triggered writer event.
	/// </summary>
	public class ModuleWriterListenerEventArgs : EventArgs {
		/// <summary>
		///     Initializes a new instance of the <see cref="ModuleWriterListenerEventArgs" /> class.
		/// </summary>
		/// <param name="evt">The triggered writer event.</param>
		public ModuleWriterListenerEventArgs(ModuleWriterEvent evt) {
			WriterEvent = evt;
		}

		/// <summary>
		///     Gets the triggered writer event.
		/// </summary>
		/// <value>The triggered writer event.</value>
		public ModuleWriterEvent WriterEvent { get; private set; }
	}
}