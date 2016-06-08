using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Confuser.Core.Project;
using ConfuserEx.Views;
using GalaSoft.MvvmLight.Command;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx.ViewModel {
	public class ProjectTabVM : TabViewModel {
		public ProjectTabVM(AppVM app)
			: base(app, "Project") { }

		public ICommand DragDrop {
			get {
				return new RelayCommand<IDataObject>(data => {
					foreach (string file in (string[])data.GetData(DataFormats.FileDrop))
						AddModule(file);
				}, data => {
					if (!data.GetDataPresent(DataFormats.FileDrop))
						return false;
					var files = (string[])data.GetData(DataFormats.FileDrop);
					bool ret = files.All(file => File.Exists(file));
					return ret;
				});
			}
		}

		public ICommand ChooseBaseDir {
			get {
				return new RelayCommand(() => {
					var fbd = new VistaFolderBrowserDialog();
					fbd.SelectedPath = App.Project.BaseDirectory;
					if (fbd.ShowDialog() ?? false) {
						App.Project.BaseDirectory = fbd.SelectedPath;
						App.Project.OutputDirectory = Path.Combine(App.Project.BaseDirectory, "Confused");
					}
				});
			}
		}

		public ICommand ChooseOutputDir {
			get {
				return new RelayCommand(() => {
					var fbd = new VistaFolderBrowserDialog();
					fbd.SelectedPath = App.Project.OutputDirectory;
					if (fbd.ShowDialog() ?? false) {
						App.Project.OutputDirectory = fbd.SelectedPath;
					}
				});
			}
		}

		public ICommand Add {
			get {
				return new RelayCommand(() => {
					var ofd = new VistaOpenFileDialog();
					ofd.Filter = ".NET assemblies (*.exe, *.dll)|*.exe;*.dll|All Files (*.*)|*.*";
					ofd.Multiselect = true;
					if (ofd.ShowDialog() ?? false) {
						foreach (var file in ofd.FileNames)
							AddModule(file);
					}
				});
			}
		}

		public ICommand Remove {
			get {
				return new RelayCommand(() => {
					Debug.Assert(App.Project.Modules.Any(m => m.IsSelected));
					string msg = "Are you sure to remove selected modules?\r\nAll settings specific to it would be lost!";
					if (MessageBox.Show(msg, "ConfuserEx", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
						foreach (var item in App.Project.Modules.Where(m => m.IsSelected).ToList())
							App.Project.Modules.Remove(item);
					}
				}, () => App.Project.Modules.Any(m => m.IsSelected));
			}
		}

		public ICommand Edit {
			get {
				return new RelayCommand(() => {
					Debug.Assert(App.Project.Modules.Count(m => m.IsSelected) == 1);
					var dialog = new ProjectModuleView(App.Project.Modules.Single(m => m.IsSelected));
					dialog.Owner = Application.Current.MainWindow;
					dialog.ShowDialog();
				}, () => App.Project.Modules.Count(m => m.IsSelected) == 1);
			}
		}

		public ICommand Advanced {
			get {
				return new RelayCommand(() => {
					var dialog = new ProjectTabAdvancedView(App.Project);
					dialog.Owner = Application.Current.MainWindow;
					dialog.ShowDialog();
				});
			}
		}

		void AddModule(string file) {
			if (!File.Exists(file)) {
				MessageBox.Show(string.Format("File '{0}' does not exists!", file), "ConfuserEx", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (string.IsNullOrEmpty(App.Project.BaseDirectory)) {
				string directory = Path.GetDirectoryName(file);
				App.Project.BaseDirectory = directory;
				App.Project.OutputDirectory = Path.Combine(directory, "Confused");
			}
			var module = new ProjectModuleVM(App.Project, new ProjectModule());
			try {
				module.Path = Confuser.Core.Utils.GetRelativePath(file, App.Project.BaseDirectory);
			}
			catch {
				module.Path = file;
			}
			App.Project.Modules.Add(module);
		}
	}
}