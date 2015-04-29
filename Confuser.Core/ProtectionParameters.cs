using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Core {
	using ProtectionParams = Dictionary<string, object>;

	/// <summary>
	///     Parameters of <see cref="ConfuserComponent" />.
	/// </summary>
	public class ProtectionParameters {
		static readonly object ParametersKey = new object();

		/// <summary>
		///     A empty instance of <see cref="ProtectionParameters" />.
		/// </summary>
		public static readonly ProtectionParameters Empty = new ProtectionParameters(null, new IDnlibDef[0]);

		readonly ConfuserComponent comp;

		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionParameters" /> class.
		/// </summary>
		/// <param name="component">The component that this parameters applied to.</param>
		/// <param name="targets">The protection targets.</param>
		internal ProtectionParameters(ConfuserComponent component, IList<IDnlibDef> targets) {
			comp = component;
			Targets = targets;
		}

		/// <summary>
		///     Gets the targets of protection.
		///     Possible targets are module, types, methods, fields, events, properties.
		/// </summary>
		/// <value>A list of protection targets.</value>
		public IList<IDnlibDef> Targets { get; private set; }


		/// <summary>
		///     Obtains the value of a parameter of the specified target.
		/// </summary>
		/// <typeparam name="T">The type of the parameter value.</typeparam>
		/// <param name="context">The working context.</param>
		/// <param name="target">The protection target.</param>
		/// <param name="name">The name of the parameter.</param>
		/// <param name="defValue">Default value if the parameter does not exist.</param>
		/// <returns>The value of the parameter.</returns>
		public T GetParameter<T>(ConfuserContext context, IDnlibDef target, string name, T defValue = default(T)) {
			Dictionary<string, string> parameters;

			if (comp == null)
				return defValue;

			if (comp is Packer && target == null) {
				// Packer parameters are stored in modules
				target = context.Modules[0];
			}

			var objParams = context.Annotations.Get<ProtectionSettings>(target, ParametersKey);
			if (objParams == null)
				return defValue;
			if (!objParams.TryGetValue(comp, out parameters))
				return defValue;

			string ret;
			if (parameters.TryGetValue(name, out ret)) {
				Type paramType = typeof(T);
				Type nullable = Nullable.GetUnderlyingType(paramType);
				if (nullable != null)
					paramType = nullable;

				if (paramType.IsEnum)
					return (T)Enum.Parse(paramType, ret, true);
				return (T)Convert.ChangeType(ret, paramType);
			}
			return defValue;
		}

		/// <summary>
		///     Sets the protection parameters of the specified target.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="target">The protection target.</param>
		/// <param name="parameters">The parameters.</param>
		public static void SetParameters(
			ConfuserContext context, IDnlibDef target, ProtectionSettings parameters) {
			context.Annotations.Set(target, ParametersKey, parameters);
		}

		/// <summary>
		///     Gets the protection parameters of the specified target.
		/// </summary>
		/// <param name="context">The context.</param>
		/// <param name="target">The protection target.</param>
		/// <returns>The parameters.</returns>
		public static ProtectionSettings GetParameters(
			ConfuserContext context, IDnlibDef target) {
			return context.Annotations.Get<ProtectionSettings>(target, ParametersKey);
		}
	}
}