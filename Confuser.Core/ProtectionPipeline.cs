using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using System.Diagnostics;

namespace Confuser.Core
{
    /// <summary>
    /// Various stages in <see cref="ProtectionPipeline"/>.
    /// </summary>
    public enum PipelineStage
    {
        /// <summary>
        /// Confuser engine inspects the loaded modules and makes necessary changes.
        /// This stage occurs only once per pipeline run.
        /// </summary>
        Inspection,

        /// <summary>
        /// Confuser engine begins to process a module.
        /// This stage occurs once per module.
        /// </summary>
        BeginModule,
        /// <summary>
        /// Confuser engine optimizes opcodes of the method bodys.
        /// This stage occurs once per module.
        /// </summary>
        OptimizeMethods,
        /// <summary>
        /// Confuser engine writes the module to byte array.
        /// This stage occurs once per module.
        /// </summary>
        WriteModule,
        /// <summary>
        /// Confuser engine finishes processing a module.
        /// This stage occurs once per module.
        /// </summary>
        EndModule,

        /// <summary>
        /// Confuser engine generates debug symbols.
        /// This stage occurs only once per pipeline.
        /// </summary>
        Debug,
        /// <summary>
        /// Confuser engine packs up the output if packer is present.
        /// This stage occurs only once per pipeline.
        /// </summary>
        Pack,
        /// <summary>
        /// Confuser engine saves the output.
        /// This stage occurs only once per pipeline.
        /// </summary>
        SaveModules
    }

    /// <summary>
    /// Protection processing pipeline.
    /// </summary>
    public class ProtectionPipeline
    {
        Dictionary<PipelineStage, List<ProtectionPhase>> preStage;
        Dictionary<PipelineStage, List<ProtectionPhase>> postStage;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectionPipeline"/> class.
        /// </summary>
        public ProtectionPipeline()
        {
            var stages = (PipelineStage[])Enum.GetValues(typeof(PipelineStage));
            preStage = stages.ToDictionary(stage => stage, stage => new List<ProtectionPhase>());
            postStage = stages.ToDictionary(stage => stage, stage => new List<ProtectionPhase>());
        }

        /// <summary>
        /// Inserts the phase into pre-processing pipeline of the specified stage.
        /// </summary>
        /// <param name="stage">The pipeline stage.</param>
        /// <param name="phase">The protection phase.</param>
        public void InsertPreStage(PipelineStage stage, ProtectionPhase phase)
        {
            preStage[stage].Add(phase);
        }

        /// <summary>
        /// Inserts the phase into post-processing pipeline of the specified stage.
        /// </summary>
        /// <param name="stage">The pipeline stage.</param>
        /// <param name="phase">The protection phase.</param>
        public void InsertPostStage(PipelineStage stage, ProtectionPhase phase)
        {
            postStage[stage].Add(phase);
        }

        /// <summary>
        /// Execute the specified pipeline stage with pre-processing and post-processing.
        /// </summary>
        /// <param name="stage">The pipeline stage.</param>
        /// <param name="func">The stage function.</param>
        /// <param name="targets">The target list of the stage.</param>
        /// <param name="context">The working context.</param>
        internal void ExecuteStage(PipelineStage stage, Action<ConfuserContext> func, Func<IList<IDefinition>> targets, ConfuserContext context)
        {
            foreach (var pre in preStage[stage])
            {
                pre.Execute(context, new ProtectionParameters(pre.Parent, Filter(context, targets(), pre)));
                context.CheckCancellation();
            }
            func(context);
            context.CheckCancellation();
            foreach (var post in postStage[stage])
            {
                post.Execute(context, new ProtectionParameters(post.Parent, Filter(context, targets(), post)));
                context.CheckCancellation();
            }
        }

        /// <summary>
        /// Returns only the targets with the specified type and used by specified component.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="targets">List of targets.</param>
        /// <param name="phase">The component phase.</param>
        /// <returns>Filtered targets.</returns>
        static IList<IDefinition> Filter(ConfuserContext context, IList<IDefinition> targets, ProtectionPhase phase)
        {
            var targetType = phase.Targets;

            IEnumerable<IDefinition> filter = targets;
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
            else
                return filter.Where(def =>
                {
                    var parameters = ProtectionParameters.GetParameters(context, def);
                    Debug.Assert(parameters != null);
                    if (parameters == null)
                    {
                        context.Logger.ErrorFormat("'{0}' not marked for obfuscation, possibly a bug.");
                        throw new ConfuserException(null);
                    }
                    return parameters.ContainsKey(phase.Parent);
                }).ToList();
        }
    }
}
