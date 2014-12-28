using System;
using System.Collections.Generic;
using Confuser.Core.Services;
using dnlib.DotNet;

namespace Confuser.Core.API {
	internal class APIStore : IAPIStore {
		readonly ConfuserContext context;
		readonly RandomGenerator random;
		readonly SortedList<int, List<IDataStore>> dataStores;
		readonly List<IOpaquePredicateDescriptor> predicates;

		/// <summary>
		///     Initializes a new instance of the <see cref="APIStore" /> class.
		/// </summary>
		/// <param name="context">The working context.</param>
		public APIStore(ConfuserContext context) {
			this.context = context;
			random = context.Registry.GetService<IRandomService>().GetRandomGenerator("APIStore");

			dataStores = new SortedList<int, List<IDataStore>>();
			predicates = new List<IOpaquePredicateDescriptor>();
		}

		/// <inheritdoc />
		public void AddStore(IDataStore dataStore) {
			dataStores.AddListEntry(dataStore.Priority, dataStore);
		}

		/// <inheritdoc />
		public void AddPredicate(IOpaquePredicateDescriptor predicate) {
			predicates.Add(predicate);
		}

		/// <inheritdoc />
		public IDataStore GetStore(MethodDef method) {
			for (int i = dataStores.Count - 1; i >= 0; i--) {
				var list = dataStores[i];
				for (int j = list.Count - 1; j >= 0; i--) {
					if (list[j].IsUsable(method))
						return list[j];
				}
			}
			return null;
		}

		/// <inheritdoc />
		public IOpaquePredicateDescriptor GetPredicate(MethodDef method, OpaquePredicateType? type, params int[] argCount) {
			var randomPredicates = predicates.ToArray();
			random.Shuffle(randomPredicates);
			foreach (var predicate in randomPredicates) {
				if (predicate.IsUsable(method) &&
				    (type == null || predicate.Type == type.Value) &&
				    (argCount == null || Array.IndexOf(argCount, predicate.ArgumentCount) != -1))
					return predicate;
			}
			return null;
		}
	}

	/// <summary>
	///     Provides storage for API interfaces
	/// </summary>
	public interface IAPIStore {
		/// <summary>
		///     Adds the specified data store into this store.
		/// </summary>
		/// <param name="dataStore">The data store.</param>
		void AddStore(IDataStore dataStore);

		/// <summary>
		///     Finds a suitable data store for the specified method, with the
		///     specified number of keys.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <returns>The suitable data store if found, or <c>null</c> if not found.</returns>
		/// <remarks>
		///     It should never returns null --- ConfuserEx has internal data store.
		/// </remarks>
		IDataStore GetStore(MethodDef method);

		/// <summary>
		///     Adds the specified opaque predicate into this store.
		/// </summary>
		/// <param name="predicate">The opaque predicate.</param>
		void AddPredicate(IOpaquePredicateDescriptor predicate);

		/// <summary>
		///     Finds a suitable opaque predicate for the specified method, with the
		///     specified properties.
		/// </summary>
		/// <param name="method">The method.</param>
		/// <param name="type">The required type of predicate, or <c>null</c> if it does not matter.</param>
		/// <param name="argCount">The required numbers of arguments, or <c>null</c> if it does not matter.</param>
		/// <returns>The suitable opaque predicate if found, or <c>null</c> if not found.</returns>
		IOpaquePredicateDescriptor GetPredicate(MethodDef method, OpaquePredicateType? type, params int[] argCount);
	}
}