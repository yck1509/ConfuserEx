using System;
using dnlib.DotNet;

namespace Confuser.Core.Services {
	internal class MarkerService : IMarkerService {
		private readonly ConfuserContext context;
		private readonly Marker marker;

		/// <summary>
		///     Initializes a new instance of the <see cref="MarkerService" /> class.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="marker">The marker.</param>
		public MarkerService(ConfuserContext context, Marker marker) {
			this.context = context;
			this.marker = marker;
		}

		/// <inheritdoc />
		public void Mark(IDnlibDef member) {
			if (member == null)
				throw new ArgumentNullException("member");
			if (member is ModuleDef)
				throw new ArgumentException("New ModuleDef cannot be marked.");
			if (IsMarked(member)) // avoid double marking
				return;

			marker.MarkMember(member, context);
		}

		/// <inheritdoc />
		public bool IsMarked(IDnlibDef def) {
			return ProtectionParameters.GetParameters(context, def) != null;
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
		/// <exception cref="System.ArgumentException"><paramref name="member" /> is a <see cref="ModuleDef" />.</exception>
		/// <exception cref="System.ArgumentNullException"><paramref name="member" /> is <c>null</c>.</exception>
		void Mark(IDnlibDef member);

		/// <summary>
		///     Determines whether the specified definition is marked.
		/// </summary>
		/// <param name="def">The definition.</param>
		/// <returns><c>true</c> if the specified definition is marked; otherwise, <c>false</c>.</returns>
		bool IsMarked(IDnlibDef def);
	}
}