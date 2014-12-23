using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;

namespace Confuser.Core {
	/// <summary>
	///     Various stages in <see cref="ProtectionPipeline" />.
	/// </summary>
	public enum PipelineStage {
		/// <summary>
		///     Confuser engine inspects the loaded modules and makes necessary changes.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		Inspection,

		/// <summary>
		///     Confuser engine begins to process a module.
		///     This stage occurs once per module.
		/// </summary>
		BeginModule,

		/// <summary>
		///     Confuser engine processes a module.
		///     This stage occurs once per module.
		/// </summary>
		ProcessModule,

		/// <summary>
		///     Confuser engine optimizes opcodes of the method bodys.
		///     This stage occurs once per module.
		/// </summary>
		OptimizeMethods,

		/// <summary>
		///     Confuser engine finishes processing a module.
		///     This stage occurs once per module.
		/// </summary>
		EndModule,

		/// <summary>
		///     Confuser engine writes the module to byte array.
		///     This stage occurs once per module, after all processing of modules are completed.
		/// </summary>
		WriteModule,

		/// <summary>
		///     Confuser engine generates debug symbols.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		Debug,

		/// <summary>
		///     Confuser engine packs up the output if packer is present.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		Pack,

		/// <summary>
		///     Confuser engine saves the output.
		///     This stage occurs only once per pipeline run.
		/// </summary>
		SaveModules
	}

	/// <summary>
	///     Protection processing pipeline.
	/// </summary>
	public class ProtectionPipeline {
		readonly Dictionary<PipelineStage, List<ProtectionPhase>> postStage;
		readonly Dictionary<PipelineStage, List<ProtectionPhase>> preStage;

		/// <summary>
		///     Initializes a new instance of the <see cref="ProtectionPipeline" /> class.
		/// </summary>
		public ProtectionPipeline() {
			var stages = (PipelineStage[])Enum.GetValues(typeof(PipelineStage));
			preStage = stages.ToDictionary(stage => stage, stage => new List<ProtectionPhase>());
			postStage = stages.ToDictionary(stage => stage, stage => new List<ProtectionPhase>());
		}

		/// <summary>
		///     Inserts the phase into pre-processing pipeline of the specified stage.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="phase">The protection phase.</param>
		public void InsertPreStage(PipelineStage stage, ProtectionPhase phase) {
			preStage[stage].Add(phase);
		}

		/// <summary>
		///     Inserts the phase into post-processing pipeline of the specified stage.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="phase">The protection phase.</param>
		public void InsertPostStage(PipelineStage stage, ProtectionPhase phase) {
			postStage[stage].Add(phase);
		}

		/// <summary>
		///     Finds the phase with the specified type in the pipeline.
		/// </summary>
		/// <typeparam name="T">The type of the phase.</typeparam>
		/// <returns>The phase with specified type in the pipeline.</returns>
		public T FindPhase<T>() where T : ProtectionPhase {
			foreach (var phases in preStage.Values)
				foreach (ProtectionPhase phase in phases) {
					if (phase is T)
						return (T)phase;
				}
			foreach (var phases in postStage.Values)
				foreach (ProtectionPhase phase in phases) {
					if (phase is T)
						return (T)phase;
				}
			return null;
		}

		/// <summary>
		///     Execute the specified pipeline stage with pre-processing and post-processing.
		/// </summary>
		/// <param name="stage">The pipeline stage.</param>
		/// <param name="func">The stage function.</param>
		/// <param name="targets">The target list of the stage.</param>
		/// <param name="context">The working context.</param>
		internal void ExecuteStage(PipelineStage stage, Action<ConfuserContext> func, Func<IList<IDnlibDef>> targets, ConfuserContext context) {
			foreach (ProtectionPhase pre in preStage[stage]) {
				context.CheckCancellation();
				context.Logger.DebugFormat("Executing '{0}' phase...", pre.Name);
				pre.Execute(context, new ProtectionParameters(pre.Parent, Filter(context, targets(), pre)));
			}
			context.CheckCancellation();
			func(context);
			context.CheckCancellation();
			foreach (ProtectionPhase post in postStage[stage]) {
				context.Logger.DebugFormat("Executing '{0}' phase...", post.Name);
				post.Execute(context, new ProtectionParameters(post.Parent, Filter(context, targets(), post)));
				context.CheckCancellation();
			}
		}

		/// <summary>
		///     Returns only the targets with the specified type and used by specified component.
		/// </summary>
		/// <param name="context">The working context.</param>
		/// <param name="targets">List of targets.</param>
		/// <param name="phase">The component phase.</param>
		/// <returns>Filtered targets.</returns>
		static IList<IDnlibDef> Filter(ConfuserContext context, IList<IDnlibDef> targets, ProtectionPhase phase) {
			ProtectionTargets targetType = phase.Targets;

			IEnumerable<IDnlibDef> filter = targets;
			if ((targetType & ProtectionTargets.Modules) == 0)
				filter = filter.Where(def => !(def is ModuleDef));
			if ((targetType & ProtectionTargets.Types) == 0)
				filter = filter.Where(def => !(def is TypeDef));
			if ((targetType & ProtectionTargets.Methods) == 0)
				filter = filter.Where(def => !(def is MethodDef));
			if ((targetType & ProtectionTargets.Fields) == 0)
				filter = filter.Where(def => !(def is FieldDef));
			if ((targetType & ProtectionTargets.Properties) == 0)
				filter = filter.Where(def => !(def is PropertyDef));
			if ((targetType & ProtectionTargets.Events) == 0)
				filter = filter.Where(def => !(def is EventDef));

			if (phase.ProcessAll)
				return filter.ToList();
			return filter.Where(def => {
				ProtectionSettings parameters = ProtectionParameters.GetParameters(context, def);
				Debug.Assert(parameters != null);
				if (parameters == null) {
					context.Logger.ErrorFormat("'{0}' not marked for obfuscation, possibly a bug.", def);
					throw new ConfuserException(null);
				}
				return parameters.ContainsKey(phase.Parent);
			}).ToList();
		}
	}
}