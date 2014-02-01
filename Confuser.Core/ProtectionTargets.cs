using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// Target components of protection.
    /// </summary>
    /// <remarks>
    /// <see cref="ProtectionTarget.Module"/> cannot be used with other flags.
    /// </remarks>
    [Flags]
    public enum ProtectionTargets
    {
        /// <summary> Type definitions. </summary>
        Types = 1,
        /// <summary> Method definitions. </summary>
        Methods = 2,
        /// <summary> Field definitions. </summary>
        Fields = 4,
        /// <summary> Event definitions. </summary>
        Events = 8,
        /// <summary> Property definitions. </summary>
        Properties = 16,
        /// <summary> All member definitions (i.e. type, methods, fields, events, properties). </summary>
        AllMembers = Types | Methods | Fields | Events | Properties,
        /// <summary> Module definition. </summary>
        Module = 32,
    }
}
