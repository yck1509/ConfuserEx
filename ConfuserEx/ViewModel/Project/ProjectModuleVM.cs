using System;
using System.Reflection;
using System.Threading;
using Confuser.Core.Project;

namespace ConfuserEx.ViewModel {
	public class ProjectModuleVM : ViewModelBase, IViewModel<ProjectModule> {
		private readonly ProjectModule module;
		private readonly ProjectVM parent;
		private string asmName = "Unknown";

		public ProjectModuleVM(ProjectVM parent, ProjectModule module) {
			this.parent = parent;
			this.module = module;
		}

		public ProjectModule Module {
			get { return module; }
		}

		public string Path {
			get { return module.Path; }
			set {
				if (SetProperty(module.Path != value, val => module.Path = val, value, "Path")) {
					parent.IsModified = true;
					LoadAssemblyName();
				}
			}
		}

		public string AssemblyName {
			get { return asmName; }
			set { SetProperty(ref asmName, value, "AssemblyName"); }
		}

		public string SNKeyPath {
			get { return module.SNKeyPath; }
			set {
				if (SetProperty(module.SNKeyPath != value, val => module.SNKeyPath = val, value, "SNKeyPath")) {
					parent.IsModified = true;
					LoadAssemblyName();
				}
			}
		}

		public string SNKeyPassword {
			get { return module.SNKeyPassword; }
			set {
				if (SetProperty(module.SNKeyPassword != value, val => module.SNKeyPassword = val, value, "SNKeyPassword")) {
					parent.IsModified = true;
					LoadAssemblyName();
				}
			}
		}

		ProjectModule IViewModel<ProjectModule>.Model {
			get { return module; }
		}

		private void LoadAssemblyName() {
			AssemblyName = "Loading...";
			ThreadPool.QueueUserWorkItem(_ => {
				try {
					string path = System.IO.Path.Combine(parent.BaseDirectory, Path);
					AssemblyName name = System.Reflection.AssemblyName.GetAssemblyName(path);
					AssemblyName = name.FullName;
				}
				catch {
					AssemblyName = "Unknown";
				}
			});
		}
	}
}