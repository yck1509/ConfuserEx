using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core
{
    using ProtectionParams = Dictionary<string, object>;

    /// <summary>
    /// Parameters of <see cref="ConfuserComponent"/>.
    /// </summary>
    public class ProtectionParameters
    {
        ConfuserComponent comp;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectionParameters" /> class.
        /// </summary>
        /// <param name="component">The component that this parameters applied to.</param>
        /// <param name="targets">The protection targets.</param>
        internal ProtectionParameters(ConfuserComponent component, IList<IDefinition> targets)
        {
            this.comp = component;
            this.Targets = targets;
        }

        /// <summary>
        /// Gets the targets of protection.
        /// Possible targets are module, types, methods, fields, events, properties.
        /// </summary>
        /// <value>A list of protection targets.</value>
        public IList<IDefinition> Targets { get; private set; }


        private static readonly object ParametersKey = new object();

        /// <summary>
        /// Obtains the value of a parameter of the specified target.
        /// </summary>
        /// <typeparam name="T">The type of the parameter value.</typeparam>
        /// <param name="context">The working context.</param>
        /// <param name="target">The protection target.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="defValue">Default value if the parameter does not exist.</param>
        /// <returns>The value of the parameter.</returns>
        public T GetParameter<T>(ConfuserContext context, IDefinition target, string name, T defValue = default(T))
        {
            var objParams = context.Annotations.Get<ProtectionSettings>(comp, ParametersKey);
            if (objParams == null)
                return defValue;
            Dictionary<string, string> protParams;
            if (!objParams.TryGetValue(comp, out protParams))
                return defValue;
            string ret;
            if (protParams.TryGetValue(name, out ret))
                return (T)Convert.ChangeType(ret, typeof(T));
            else
                return defValue;
        }

        /// <summary>
        /// Sets the protection parameters of the specified target.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="target">The protection target.</param>
        /// <param name="parameters">The parameters.</param>
        internal static void SetParameters(
            ConfuserContext context, IDefinition target, ProtectionSettings parameters)
        {
            context.Annotations.Set(target, ParametersKey, parameters);
        }

        /// <summary>
        /// Gets the protection parameters of the specified target.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="target">The protection target.</param>
        /// <returns>The parameters.</returns>
        internal static ProtectionSettings GetParameters(
            ConfuserContext context, IDefinition target)
        {
            return context.Annotations.Get<ProtectionSettings>(target, ParametersKey);
        }
    }
}
