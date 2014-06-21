using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ConfuserEx.ViewModel;

namespace ConfuserEx.Views {
	public partial class ProjectRuleView : Window {
		private readonly ProjectRuleVM rule;

		public ProjectRuleView(ProjectRuleVM rule) {
			InitializeComponent();
			this.rule = rule;
			DataContext = rule;

			rule.PropertyChanged += OnPropertyChanged;
			CheckValidity();
		}

		public void Cleanup() {
			rule.PropertyChanged -= OnPropertyChanged;
		}

		private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
			if (e.PropertyName == "Expression")
				CheckValidity();
		}

		private void CheckValidity() {
			if (rule.Expression == null)
				pattern.BorderBrush = Brushes.Red;
			else
				pattern.ClearValue(BorderBrushProperty);
		}
	}
}