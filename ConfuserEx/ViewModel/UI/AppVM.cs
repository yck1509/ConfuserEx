using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Confuser.Core;

namespace ConfuserEx.ViewModel {
	public class AppVM : ViewModelBase {
		private readonly IList<TabViewModel> tabs = new ObservableCollection<TabViewModel>();
		private string fileName;
		private bool navDisabled;

		private ProjectVM proj;

		public bool NavigationDisabled {
			get { return navDisabled; }
			set { SetProperty(ref navDisabled, value, "NavigationDisabled"); }
		}

		public ProjectVM Project {
			get { return proj; }
			set {
				if (proj != null)
					proj.PropertyChanged -= OnProjectPropertyChanged;

				SetProperty(ref proj, value, "Project");

				if (proj != null)
					proj.PropertyChanged += OnProjectPropertyChanged;
			}
		}

		public string FileName {
			get { return fileName; }
			set {
				SetProperty(ref fileName, value, "Project");
				OnPropertyChanged("Title");
			}
		}

		public string Title {
			get {
				return string.Format("{0}{1} - {2}",
				                     Path.GetFileName(fileName),
				                     (proj.IsModified ? "*" : ""),
				                     ConfuserEngine.Version);
			}
		}

		public IList<TabViewModel> Tabs {
			get { return tabs; }
		}

		private void OnProjectPropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "IsModified" && proj.IsModified)
				OnPropertyChanged("Title");
		}
	}
}