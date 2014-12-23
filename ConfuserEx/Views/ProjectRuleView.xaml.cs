using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Confuser.Core;
using Confuser.Core.Project;
using ConfuserEx.ViewModel;
using GalaSoft.MvvmLight.Command;

namespace ConfuserEx.Views {
	public partial class ProjectRuleView : Window {
		readonly ProjectVM proj;
		readonly ProjectRuleVM rule;

		public ProjectRuleView(ProjectVM proj, ProjectRuleVM rule) {
			InitializeComponent();
			this.rule = rule;
			this.proj = proj;
			DataContext = rule;

			rule.PropertyChanged += OnPropertyChanged;
			CheckValidity();
		}

		public ProjectVM Project {
			get { return proj; }
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();

			AddBtn.Command = new RelayCommand(() => {
				var prot = new ProjectSettingVM<Protection>(proj, new SettingItem<Protection>());
				prot.Id = proj.Protections[0].Id;
				rule.Protections.Add(prot);
			});
			RemoveBtn.Command = new RelayCommand(() => {
				int selIndex = prots.SelectedIndex;
				Debug.Assert(selIndex != -1);

				rule.Protections.RemoveAt(prots.SelectedIndex);
				prots.SelectedIndex = selIndex >= rule.Protections.Count ? rule.Protections.Count - 1 : selIndex;
			}, () => prots.SelectedIndex != -1);
		}

		public void Cleanup() {
			rule.PropertyChanged -= OnPropertyChanged;
		}

		void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "Expression")
				CheckValidity();
		}

		void CheckValidity() {
			if (rule.Expression == null) {
				pattern.BorderBrush = Brushes.Red;
				errorImg.Visibility = Visibility.Visible;
			}
			else {
				pattern.ClearValue(BorderBrushProperty);
				errorImg.Visibility = Visibility.Hidden;
			}
		}

		void Done(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}
	}
}