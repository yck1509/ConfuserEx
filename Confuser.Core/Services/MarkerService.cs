using System;
using System.Collections.Generic;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	internal class MarkerService : IMarkerService {
		readonly ConfuserContext context;
		readonly Marker marker;
		readonly Dictionary<IDnlibDef, ConfuserComponent> helperParents;

		/// <summary>
		///     Initializes a new instance of the <see cref="MarkerService" /> class.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="marker">The marker.</param>
		public MarkerService(ConfuserContext context, Marker marker) {
			this.context = context;
			this.marker = marker;
			helperParents = new Dictionary<IDnlibDef, ConfuserComponent>();
		}

		/// <inheritdoc />
		public void Mark(IDnlibDef member, ConfuserComponent parentComp) {
			if (member == null)
				throw new ArgumentNullException("member");
			if (member is ModuleDef)
				throw new ArgumentException("New ModuleDef cannot be marked.");
			if (IsMarked(member)) // avoid double marking
				return;

			marker.MarkMember(member, context);
			if (parentComp != null)
				helperParents[member] = parentComp;
		}

		/// <inheritdoc />
		public bool IsMarked(IDnlibDef def) {
			return ProtectionParameters.GetParameters(context, def) != null;
		}

		/// <inheritdoc />
		public ConfuserComponent GetHelperParent(IDnlibDef def) {
			ConfuserComponent parent;
			if (!helperParents.TryGetValue(def, out parent))
				return null;
			return parent;
		}
	}

	/// <summary>
	///     Provides methods to access the obfuscation marker.
	/// </summary>
	public interface IMarkerService {
		/// <summary>
		///     Marks the helper member.
		/// </summary>
		/// <param name="member">The helper member.</param>
		/// <param name="parentComp">The parent component.</param>
		/// <exception cref="System.ArgumentException"><paramref name="member" /> is a <see cref="ModuleDef" />.</exception>
		/// <exception cref="System.ArgumentNullException"><paramref name="member" /> is <c>null</c>.</exception>
		void Mark(IDnlibDef member, ConfuserComponent parentComp);

		/// <summary>
		///     Determines whether the specified definition is marked.
		/// </summary>
		/// <param name="def">The definition.</param>
		/// <returns><c>true</c> if the specified definition is marked; otherwise, <c>false</c>.</returns>
		bool IsMarked(IDnlibDef def);

		/// <summary>
		///     Gets the parent component of the specified helper.
		/// </summary>
		/// <param name="def">The helper definition.</param>
		/// <returns>The parent component of the helper, or <c>null</c> if the specified definition is not a helper.</returns>
		ConfuserComponent GetHelperParent(IDnlibDef def);
	}
}