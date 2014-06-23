using System;
using Confuser.Core;

namespace Confuser.Renamer {
	public interface INameReference {
		bool UpdateNameReference(ConfuserContext context, INameService service);

		bool ShouldCancelRename();
	}

	public interface INameReference<out T> : INameReference { }
}