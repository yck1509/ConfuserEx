using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ConfuserEx.ViewModel {
	public static class Utils {
		public static ObservableCollection<T> Wrap<T>(IList<T> list) {
			var ret = new ObservableCollection<T>(list);

			ret.CollectionChanged += (sender, e) => {
				var collection = (ObservableCollection<T>)sender;
				switch (e.Action) {
					case NotifyCollectionChangedAction.Reset:
						list.Clear();
						foreach (T item in collection)
							list.Add(item);
						break;

					case NotifyCollectionChangedAction.Add:
						for (int i = 0; i < e.NewItems.Count; i++)
							list.Insert(e.NewStartingIndex + i, (T)e.NewItems[i]);
						break;

					case NotifyCollectionChangedAction.Remove:
						for (int i = 0; i < e.OldItems.Count; i++)
							list.RemoveAt(e.OldStartingIndex);
						break;

					case NotifyCollectionChangedAction.Move:
						list.RemoveAt(e.OldStartingIndex);
						list.Insert(e.NewStartingIndex, (T)e.NewItems[0]);
						break;

					case NotifyCollectionChangedAction.Replace:
						list[e.NewStartingIndex] = (T)e.NewItems[0];
						break;
				}
			};
			return ret;
		}

		public static ObservableCollection<TViewModel> Wrap<TModel, TViewModel>(IList<TModel> list, Func<TModel, TViewModel> transform) where TViewModel : IViewModel<TModel> {
			var ret = new ObservableCollection<TViewModel>(list.Select(item => transform(item)));

			ret.CollectionChanged += (sender, e) => {
				var collection = (ObservableCollection<TViewModel>)sender;
				switch (e.Action) {
					case NotifyCollectionChangedAction.Reset:
						list.Clear();
						foreach (TViewModel item in collection)
							list.Add(item.Model);
						break;

					case NotifyCollectionChangedAction.Add:
						for (int i = 0; i < e.NewItems.Count; i++)
							list.Insert(e.NewStartingIndex + i, ((TViewModel)e.NewItems[i]).Model);
						break;

					case NotifyCollectionChangedAction.Remove:
						for (int i = 0; i < e.OldItems.Count; i++)
							list.RemoveAt(e.OldStartingIndex);
						break;

					case NotifyCollectionChangedAction.Move:
						list.RemoveAt(e.OldStartingIndex);
						list.Insert(e.NewStartingIndex, ((TViewModel)e.NewItems[0]).Model);
						break;

					case NotifyCollectionChangedAction.Replace:
						list[e.NewStartingIndex] = ((TViewModel)e.NewItems[0]).Model;
						break;
				}
			};
			return ret;
		}
	}
}