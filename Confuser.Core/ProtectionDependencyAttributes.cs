using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// Indicates the <see cref="Protection"/> must initialize before the specified protections.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    class BeforeProtectionAttribute : Attribute
    {
        /// <summary>
        /// Gets the full IDs of the specified protections.
        /// </summary>
        /// <value>The IDs of protections.</value>
        public string[] Ids { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BeforeProtectionAttribute"/> class.
        /// </summary>
        /// <param name="ids">The full IDs of the specified protections.</param>
        public BeforeProtectionAttribute(params string[] ids)
        {
            this.Ids = ids;
        }
    }

    /// <summary>
    /// Indicates the <see cref="Protection"/> must initialize after the specified protections.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    class AfterProtectionAttribute : Attribute
    {
        /// <summary>
        /// Gets the full IDs of the specified protections.
        /// </summary>
        /// <value>The IDs of protections.</value>
        public string[] Ids { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BeforeProtectionAttribute"/> class.
        /// </summary>
        /// <param name="ids">The full IDs of the specified protections.</param>
        public AfterProtectionAttribute(params string[] ids)
        {
            this.Ids = ids;
        }
    }
}
