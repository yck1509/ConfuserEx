using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ConfuserEx.ViewModel {
	public class ViewModelBase : INotifyPropertyChanged {
		// http://stackoverflow.com/a/1316417/462805

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string property) {
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(property));
		}

		protected bool SetProperty<T>(ref T field, T value, string property) {
			if (!EqualityComparer<T>.Default.Equals(field, value)) {
				field = value;
				OnPropertyChanged(property);
				return true;
			}
			return false;
		}

		protected bool SetProperty<T>(bool changed, Action<T> setter, T value, string property) {
			if (changed) {
				setter(value);
				OnPropertyChanged(property);
				return true;
			}
			return false;
		}
	}
}