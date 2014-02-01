using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Confuser.Core
{
    /// <summary>
    /// Parameters of protection.
    /// </summary>
    public class ProtectionParameters
    {
        /// <summary>
        /// Gets the targets components of protection.
        /// Possible targets are module, types, methods, fields, events, properties.
        /// </summary>
        /// <value>The target protection components.</value>
        public IList<IMDTokenProvider> ProtectionTargets { get; private set; }

        Dictionary<string, string> parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Obtains the value of a parameter.
        /// </summary>
        /// <typeparam name="T">The type of the parameter value.</typeparam>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="defValue">Default value if the parameter does not exist.</param>
        /// <returns>The value of the parameter.</returns>
        public T GetParameter<T>(string name, T defValue = default(T))
        {
            string ret;
            if (!parameters.TryGetValue(name, out ret))
                return defValue;
            return (T)Convert.ChangeType(ret, typeof(T));
        }
    }
}
