using System;

namespace Confuser.Renamer.BAML {
	internal interface IBAMLReference {
		bool CanRename(string oldName, string newName);
		void Rename(string oldName, string newName);
	}
}