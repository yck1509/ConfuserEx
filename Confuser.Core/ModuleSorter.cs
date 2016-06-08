using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	///     Sort modules according dependencies.
	/// </summary>
	internal class ModuleSorter {
		readonly List<ModuleDefMD> modules;

		public ModuleSorter(IEnumerable<ModuleDefMD> modules) {
			this.modules = modules.ToList();
		}

		public IList<ModuleDefMD> Sort() {
			var edges = new List<DependencyGraphEdge>();
			var roots = new HashSet<ModuleDefMD>(modules);
			var asmMap = modules.GroupBy(module => module.Assembly.ToAssemblyRef(), AssemblyNameComparer.CompareAll)
			                    .ToDictionary(gp => gp.Key, gp => gp.ToList(), AssemblyNameComparer.CompareAll);

			foreach (ModuleDefMD m in modules)
				foreach (AssemblyRef nameRef in m.GetAssemblyRefs()) {
					if (!asmMap.ContainsKey(nameRef))
						continue;

					foreach (var asmModule in asmMap[nameRef])
						edges.Add(new DependencyGraphEdge(asmModule, m));
					roots.Remove(m);
				}

			var sorted = SortGraph(roots, edges).ToList();
			Debug.Assert(sorted.Count == modules.Count);
			return sorted;
		}

		IEnumerable<ModuleDefMD> SortGraph(IEnumerable<ModuleDefMD> roots, IList<DependencyGraphEdge> edges) {
			var visited = new HashSet<ModuleDefMD>();
			var queue = new Queue<ModuleDefMD>(roots);
			do {
				while (queue.Count > 0) {
					var node = queue.Dequeue();
					visited.Add(node);

					Debug.Assert(!edges.Where(edge => edge.To == node).Any());
					yield return node;

					foreach (DependencyGraphEdge edge in edges.Where(edge => edge.From == node).ToList()) {
						edges.Remove(edge);
						if (!edges.Any(e => e.To == edge.To))
							queue.Enqueue(edge.To);
					}
				}
				if (edges.Count > 0) {
					foreach (var edge in edges) {
						if (!visited.Contains(edge.From)) {
							queue.Enqueue(edge.From);
							break;
						}
					}
				}
			} while (edges.Count > 0);
		}

		class DependencyGraphEdge {
			public DependencyGraphEdge(ModuleDefMD from, ModuleDefMD to) {
				From = from;
				To = to;
			}

			public ModuleDefMD From { get; private set; }
			public ModuleDefMD To { get; private set; }
		}
	}
}