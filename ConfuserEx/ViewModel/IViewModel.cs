using System;

namespace ConfuserEx.ViewModel {
	public interface IViewModel<TModel> {
		TModel Model { get; }
	}
}