using System;
using Confuser.Core.Project;

namespace ConfuserEx.ViewModel {
	public class ProjectSettingVM<T> : ViewModelBase, IViewModel<SettingItem<T>> {
		readonly ProjectVM parent;
		readonly SettingItem<T> setting;

		public ProjectSettingVM(ProjectVM parent, SettingItem<T> setting) {
			this.parent = parent;
			this.setting = setting;
		}

		public string Id {
			get { return setting.Id; }
			set {
				if (SetProperty(setting.Id != value, val => setting.Id = val, value, "Id"))
					parent.IsModified = true;
			}
		}

		public SettingItemAction Action {
			get { return setting.Action; }
			set {
				if (SetProperty(setting.Action != value, val => setting.Action = val, value, "Action"))
					parent.IsModified = true;
			}
		}

		SettingItem<T> IViewModel<SettingItem<T>>.Model {
			get { return setting; }
		}
	}
}