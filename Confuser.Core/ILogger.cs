using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core
{
    /// <summary>
    /// Defines a logger used to log Confuser events
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a message at DEBUG level.
        /// </summary>
        /// <param name="msg">The message.</param>
        void Debug(string msg);

        /// <summary>
        /// Logs a message at DEBUG level with specified parameters.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        void DebugFormat(string format, params object[] args);

        /// <summary>
        /// Logs a message at INFO level.
        /// </summary>
        /// <param name="msg">The message.</param>
        void Info(string msg);

        /// <summary>
        /// Logs a message at INFO level with specified parameters.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        void InfoFormat(string format, params object[] args);

        /// <summary>
        /// Logs a message at WARN level.
        /// </summary>
        /// <param name="msg">The message.</param>
        void Warn(string msg);

        /// <summary>
        /// Logs a message at WARN level with specified parameters.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        void WarnFormat(string format, params object[] args);

        /// <summary>
        /// Logs a message at WARN level with specified exception.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="ex">The exception.</param>
        void WarnException(string msg, Exception ex);

        /// <summary>
        /// Logs a message at ERROR level.
        /// </summary>
        /// <param name="msg">The message.</param>
        void Error(string msg);

        /// <summary>
        /// Logs a message at ERROR level with specified parameters.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        void ErrorFormat(string format, params object[] args);

        /// <summary>
        /// Logs a message at ERROR level with specified exception.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="ex">The exception.</param>
        void ErrorException(string msg, Exception ex);

        /// <summary>
        /// Logs the progress of protection.
        /// </summary>
        /// <param name="overall">The total work amount .</param>
        /// <param name="progress">The amount of work done.</param>
        void Progress(int overall, int progress);

        /// <summary>
        /// Logs the beginning of protection of the module.
        /// </summary>
        /// <remarks>
        /// This method may not be called on modules that the protection is
        /// not yet started.
        /// </remarks>
        /// <param name="module">The module.</param>
        void BeginModule(ModuleDef module);

        /// <summary>
        /// Logs the ending of protection of the module.
        /// </summary>
        /// <remarks>
        /// This method may not be called on modules that the protection is
        /// interrupted due to error or cancellation.
        /// </remarks>
        /// <param name="module">The module.</param>
        void EndModule(ModuleDef module);

        /// <summary>
        /// Logs the finish of protection.
        /// </summary>
        /// <param name="successful">Indicated whether the protection process is successful.</param>
        void Finish(bool successful);
    }
}
