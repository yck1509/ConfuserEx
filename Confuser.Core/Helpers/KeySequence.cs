using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core.Services;
using dnlib.DotNet.Emit;

namespace Confuser.Core.Helpers {
	/// <summary>
	///     The type of block in the key sequence
	/// </summary>
	public enum BlockKeyType {
		/// <summary>
		///     The state key should be explicitly set in the block
		/// </summary>
		Explicit,

		/// <summary>
		///     The state key could be assumed to be same as <see cref="BlockKey.EntryState" /> at the beginning of block.
		/// </summary>
		Incremental
	}

	/// <summary>
	///     The information of the block in the key sequence
	/// </summary>
	public struct BlockKey {
		/// <summary>
		///     The state key at the beginning of the block
		/// </summary>
		public uint EntryState;

		/// <summary>
		///     The state key at the end of the block
		/// </summary>
		public uint ExitState;

		/// <summary>
		///     The type of block
		/// </summary>
		public BlockKeyType Type;
	}

	/// <summary>
	///     Computes a key sequence that is valid according to the execution of the CFG.
	/// </summary>
	/// <remarks>
	///     The caller can utilize the information provided by this classes to instruments state machines.
	///     For example:
	///     <code>
	/// int state = 4;
	/// for (int i = 0 ; i &lt; 10; i++) {
	///     state = 6;
	///     if (i % 2 == 0) {
	///         state = 3;
	///     else {
	///         // The state varaible is guaranteed to be 6 in here.
	///     }
	/// }
	///     </code>
	/// </remarks>
	public static class KeySequence {
		/// <summary>
		///     Computes a key sequence of the given CFG.
		/// </summary>
		/// <param name="graph">The CFG.</param>
		/// <param name="random">The random source, or <c>null</c> if key id is needed.</param>
		/// <returns>The generated key sequence of the CFG.</returns>
		public static BlockKey[] ComputeKeys(ControlFlowGraph graph, RandomGenerator random) {
			var keys = new BlockKey[graph.Count];

			foreach (ControlFlowBlock block in graph) {
				var key = new BlockKey();
				if ((block.Type & ControlFlowBlockType.Entry) != 0)
					key.Type = BlockKeyType.Explicit;
				else
					key.Type = BlockKeyType.Incremental;
				keys[block.Id] = key;
			}
			ProcessBlocks(keys, graph, random);
			return keys;
		}

		static void ProcessBlocks(BlockKey[] keys, ControlFlowGraph graph, RandomGenerator random) {
			uint id = 0;
			for (int i = 0; i < keys.Length; i++) {
				keys[i].EntryState = id++;
				keys[i].ExitState = id++;
			}

			var finallyIds = new Dictionary<ExceptionHandler, uint>();
			var ehMap = new Dictionary<ControlFlowBlock, List<ExceptionHandler>>();

			bool updated;
			do {
				updated = false;

				// Update the state ids with the maximum id
				foreach (ControlFlowBlock block in graph) {
					BlockKey key = keys[block.Id];
					if (block.Sources.Count > 0) {
						uint newEntry = block.Sources.Select(b => keys[b.Id].ExitState).Max();
						if (key.EntryState != newEntry) {
							key.EntryState = newEntry;
							updated = true;
						}
					}
					if (block.Targets.Count > 0) {
						uint newExit = block.Targets.Select(b => keys[b.Id].EntryState).Max();
						if (key.ExitState != newExit) {
							key.ExitState = newExit;
							updated = true;
						}
					}
					if (block.Footer.OpCode.Code == Code.Endfilter || block.Footer.OpCode.Code == Code.Endfinally) {
						// Match the exit state within finally/fault/filter
						List<ExceptionHandler> ehs;
						if (!ehMap.TryGetValue(block, out ehs)) {
							ehs = new List<ExceptionHandler>();
							int footerIndex = graph.IndexOf(block.Footer);
							foreach (var eh in graph.Body.ExceptionHandlers) {
								if (eh.FilterStart != null && block.Footer.OpCode.Code == Code.Endfilter) {
									if (footerIndex >= graph.IndexOf(eh.FilterStart) &&
									    footerIndex < graph.IndexOf(eh.HandlerStart))
										ehs.Add(eh);
								}
								else if (eh.HandlerType == ExceptionHandlerType.Finally ||
								         eh.HandlerType == ExceptionHandlerType.Fault) {
									if (footerIndex >= graph.IndexOf(eh.HandlerStart) &&
									    (eh.HandlerEnd == null || footerIndex < graph.IndexOf(eh.HandlerEnd)))
										ehs.Add(eh);
								}
							}
							ehMap[block] = ehs;
						}
						foreach (var eh in ehs) {
							uint ehVal;
							if (finallyIds.TryGetValue(eh, out ehVal)) {
								if (key.ExitState > ehVal) {
									finallyIds[eh] = key.ExitState;
									updated = true;
								}
								else if (key.ExitState < ehVal) {
									key.ExitState = ehVal;
									updated = true;
								}
							}
							else {
								finallyIds[eh] = key.ExitState;
								updated = true;
							}
						}
					}
					else if (block.Footer.OpCode.Code == Code.Leave || block.Footer.OpCode.Code == Code.Leave_S) {
						// Match the exit state with finally/fault/filter
						List<ExceptionHandler> ehs;
						if (!ehMap.TryGetValue(block, out ehs)) {
							ehs = new List<ExceptionHandler>();
							int footerIndex = graph.IndexOf(block.Footer);
							foreach (var eh in graph.Body.ExceptionHandlers) {
								if (footerIndex >= graph.IndexOf(eh.TryStart) &&
								    (eh.TryEnd == null || footerIndex < graph.IndexOf(eh.TryEnd)))
									ehs.Add(eh);
							}
							ehMap[block] = ehs;
						}

						uint? maxVal = null;
						foreach (var eh in ehs) {
							uint ehVal;
							if (finallyIds.TryGetValue(eh, out ehVal) && (maxVal == null || ehVal > maxVal)) {
								if (maxVal != null)
									updated = true;
								maxVal = ehVal;
							}
						}
						if (maxVal != null) {
							if (key.ExitState > maxVal.Value) {
								maxVal = key.ExitState;
								updated = true;
							}
							else if (key.ExitState < maxVal.Value) {
								key.ExitState = maxVal.Value;
								updated = true;
							}
							foreach (var eh in ehs)
								finallyIds[eh] = maxVal.Value;
						}
					}
					keys[block.Id] = key;
				}
			} while (updated);

			if (random != null) {
				// Replace id with actual values
				var idMap = new Dictionary<uint, uint>();
				for (int i = 0; i < keys.Length; i++) {
					BlockKey key = keys[i];

					uint entryId = key.EntryState;
					if (!idMap.TryGetValue(entryId, out key.EntryState))
						key.EntryState = idMap[entryId] = random.NextUInt32();

					uint exitId = key.ExitState;
					if (!idMap.TryGetValue(exitId, out key.ExitState))
						key.ExitState = idMap[exitId] = random.NextUInt32();

					keys[i] = key;
				}
			}
		}
	}
}