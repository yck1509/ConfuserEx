using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// The Core namespace.
/// </summary>
namespace Confuser.Core
{
    /// <summary>
    /// Base class of Confuser protections.
    /// </summary>
    /// <remarks>
    /// A parameterless constructor must exists in derived classes to enable plugin discovery.
    /// </remarks>
    public abstract class Protection
    {
        /// <summary>
        /// Resets the state of protection.
        /// </summary>
        internal protected abstract void Reset();

        /// <summary>
        /// Initializes the protection.
        /// </summary>
        internal protected abstract void Initialize();

        /// <summary>
        /// Inserts protection stages into processing pipeline.
        /// </summary>
        /// <param name="pipeline">The processing pipeline.</param>
        internal protected abstract void InitPipeline(ProtectionPipeline pipeline);

        /// <summary>
        /// Gets the name of protection.
        /// </summary>
        /// <value>The name of protection.</value>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the description of protection.
        /// </summary>
        /// <value>The description of protection.</value>
        public abstract string Description { get; }

        /// <summary>
        /// Gets the identifier of protection used by users.
        /// </summary>
        /// <value>The identifier of protection.</value>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the full identifier of protection used in Confuser.
        /// </summary>
        /// <value>The full identifier of protection.</value>
        public abstract string FullId { get; }

        /// <summary>
        /// Gets the target components of protection.
        /// </summary>
        /// <value>The target components of protection.</value>
        public abstract ProtectionTargets Target { get; }

        /// <summary>
        /// Gets the preset this protection is in.
        /// </summary>
        /// <value>The protection's preset.</value>
        public abstract ProtectionPreset Preset { get; }
    }
}
