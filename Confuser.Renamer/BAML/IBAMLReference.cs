using System;

namespace Confuser.Renamer.BAML {
	internal interface IBAMLReference {
		void Rename(string oldName, string newName);
	}
}