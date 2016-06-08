using System;
using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Confuser.Core;
using Confuser.Core.Project;
using ConfuserEx.Views;
using GalaSoft.MvvmLight.Command;

namespace ConfuserEx.ViewModel {
	internal class SettingsTabVM : TabViewModel {
		bool hasPacker;
		IRuleContainer selectedList;
		int selectedRuleIndex;

		public SettingsTabVM(AppVM app)
			: base(app, "Settings") {
			app.PropertyChanged += (sender, e) => {
				if (e.PropertyName == "Project")
					InitProject();
			};
			InitProject();
		}

		public bool HasPacker {
			get { return hasPacker; }
			set { SetProperty(ref hasPacker, value, "HasPacker"); }
		}

		public IList ModulesView { get; private set; }

		public IRuleContainer SelectedList {
			get { return selectedList; }
			set {
				if (SetProperty(ref selectedList, value, "SelectedList"))
					SelectedRuleIndex = -1;
			}
		}

		public int SelectedRuleIndex {
			get { return selectedRuleIndex; }
			set { SetProperty(ref selectedRuleIndex, value, "SelectedRuleIndex"); }
		}

		public ICommand Add {
			get {
				return new RelayCommand(() => {
					Debug.Assert(SelectedList != null);

					var rule = new ProjectRuleVM(App.Project, new Rule());
					rule.Pattern = "true";
					SelectedList.Rules.Add(rule);
					SelectedRuleIndex = SelectedList.Rules.Count - 1;
				}, () => SelectedList != null);
			}
		}

		public ICommand Remove {
			get {
				return new RelayCommand(() => {
					int selIndex = SelectedRuleIndex;
					Debug.Assert(SelectedList != null);
					Debug.Assert(selIndex != -1);

					ProjectRuleVM rule = SelectedList.Rules[selIndex];
					SelectedList.Rules.RemoveAt(selIndex);
					SelectedRuleIndex = selIndex >= SelectedList.Rules.Count ? SelectedList.Rules.Count - 1 : selIndex;
				}, () => SelectedRuleIndex != -1 && SelectedList != null);
			}
		}

		public ICommand Edit {
			get {
				return new RelayCommand(() => {
					Debug.Assert(SelectedRuleIndex != -1);
					var dialog = new ProjectRuleView(App.Project, SelectedList.Rules[SelectedRuleIndex]);
					dialog.Owner = Application.Current.MainWindow;
					dialog.ShowDialog();
					dialog.Cleanup();
				}, () => SelectedRuleIndex != -1 && SelectedList != null);
			}
		}

		void InitProject() {
			ModulesView = new CompositeCollection {
				App.Project,
				new CollectionContainer { Collection = App.Project.Modules }
			};
			OnPropertyChanged("ModulesView");
			HasPacker = App.Project.Packer != null;
		}

		protected override void OnPropertyChanged(string property) {
			if (property == "HasPacker") {
				if (hasPacker && App.Project.Packer == null)
					App.Project.Packer = new ProjectSettingVM<Packer>(App.Project, new SettingItem<Packer> { Id = App.Project.Packers[0].Id });
				else if (!hasPacker)
					App.Project.Packer = null;
			}
			base.OnPropertyChanged(property);
		}
	}
}