using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Confuser.Core
{
    /// <summary>
    /// Various stages in <see cref="ProtectionPipeline"/>.
    /// </summary>
    public enum PipelineStage
    {
        /// <summary>
        /// Confuser engine loads modules from the sources and creates <see cref="ProtectionContext"/>.
        /// This stage occurs only once per pipeline run.
        /// </summary>
        LoadModules,
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
        Dictionary<Protection, ProtectionParameters> protectionParams;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProtectionPipeline"/> class.
        /// </summary>
        /// <param name="parameters">The protection parameters.</param>
        public ProtectionPipeline(Dictionary<Protection, ProtectionParameters> parameters)
        {
            var stages = (PipelineStage[])Enum.GetValues(typeof(PipelineStage));
            preStage = stages.ToDictionary(stage => stage, stage => new List<ProtectionPhase>());
            postStage = stages.ToDictionary(stage => stage, stage => new List<ProtectionPhase>());
            this.protectionParams = parameters;
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
        /// Execute the specified pipeline stage with pre-processing & post-processing.
        /// </summary>
        /// <param name="stage">The pipeline stage.</param>
        /// <param name="phase">The stage function.</param>
        internal void ExecuteStage(PipelineStage stage, Action func, ProtectionContext context)
        {
            foreach (var pre in preStage[stage])
                pre.Execute(context, protectionParams[pre.Parent]);
            func();
            foreach (var post in postStage[stage])
                post.Execute(context, protectionParams[post.Parent]);
        }
    }
}
