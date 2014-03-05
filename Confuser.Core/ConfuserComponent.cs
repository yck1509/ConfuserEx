using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// Represent a component in Confuser
    /// </summary>
    public abstract class ConfuserComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfuserComponent"/> class.
        /// </summary>
        public ConfuserComponent() { }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        /// <param name="context">The working context.</param>
        internal protected abstract void Initialize(ConfuserContext context);

        /// <summary>
        /// Inserts protection stages into processing pipeline.
        /// </summary>
        /// <param name="pipeline">The processing pipeline.</param>
        internal protected abstract void PopulatePipeline(ProtectionPipeline pipeline);

        /// <summary>
        /// Gets the name of component.
        /// </summary>
        /// <value>The name of component.</value>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the description of component.
        /// </summary>
        /// <value>The description of component.</value>
        public abstract string Description { get; }

        /// <summary>
        /// Gets the identifier of component used by users.
        /// </summary>
        /// <value>The identifier of component.</value>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the full identifier of component used in Confuser.
        /// </summary>
        /// <value>The full identifier of component.</value>
        public abstract string FullId { get; }
    }
}
