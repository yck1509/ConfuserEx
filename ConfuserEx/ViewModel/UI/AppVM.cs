using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using Confuser.Core;
using Confuser.Core.Project;
using GalaSoft.MvvmLight.Command;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx.ViewModel {
	public class AppVM : ViewModelBase {

		private readonly IList<TabViewModel> tabs = new ObservableCollection<TabViewModel>();
		private string fileName;
		private bool navDisabled;
		private bool firstSaved;

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

		public ICommand NewProject {
			get { return new RelayCommand(NewProj, () => !NavigationDisabled); }
		}

		public ICommand OpenProject {
			get { return new RelayCommand(OpenProj, () => !NavigationDisabled); }
		}

		public ICommand SaveProject {
			get { return new RelayCommand(() => SaveProj(), () => !NavigationDisabled); }
		}

		public ICommand Decode {
			get { return new RelayCommand(() => new StackTraceDecoder { Owner = Application.Current.MainWindow }.ShowDialog(), () => !NavigationDisabled); }
		}

		public bool OnWindowClosing() {
			return PromptSave();
		}

		private bool SaveProj() {
			if (!firstSaved || !File.Exists(FileName)) {
				var sfd = new VistaSaveFileDialog();
				sfd.FileName = FileName;
				sfd.Filter = "ConfuserEx Projects (*.crproj)|*.crproj|All Files (*.*)|*.*";
				if (!(sfd.ShowDialog(Application.Current.MainWindow) ?? false) || sfd.FileName == null)
					return false;
				FileName = sfd.FileName;
			}
			ConfuserProject proj = ((IViewModel<ConfuserProject>)Project).Model;
			proj.Save().Save(FileName);
			Project.IsModified = false;
			firstSaved = true;
			return true;
		}

		private bool PromptSave() {
			if (!Project.IsModified)
				return true;
			switch (MessageBox.Show("The current project has unsaved changes. Do you want to save them?", "ConfuserEx", MessageBoxButton.YesNoCancel, MessageBoxImage.Question)) {
				case MessageBoxResult.Yes:
					return SaveProj();
				case MessageBoxResult.No:
					return true;
				case MessageBoxResult.Cancel:
					return false;
			}
			return false;
		}

		private void NewProj() {
			if (!PromptSave())
				return;

			Project = new ProjectVM(new ConfuserProject());
			FileName = "Unnamed.crproj";
		}

		private void OpenProj() {
			if (!PromptSave())
				return;

			var ofd = new VistaOpenFileDialog();
			ofd.Filter = "ConfuserEx Projects (*.crproj)|*.crproj|All Files (*.*)|*.*";
			if ((ofd.ShowDialog(Application.Current.MainWindow) ?? false) && ofd.FileName != null) {
				string fileName = ofd.FileName;
				try {
					var xmlDoc = new XmlDocument();
					xmlDoc.Load(fileName);
					var proj = new ConfuserProject();
					proj.Load(xmlDoc);
					Project = new ProjectVM(proj);
					FileName = fileName;
				}
				catch {
					MessageBox.Show("Invalid project!", "ConfuserEx", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
		}

		private void OnProjectPropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "IsModified")
				OnPropertyChanged("Title");
		}

	}
}