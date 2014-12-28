using System;
using Confuser.Core.API;
using Confuser.Core.Services;

namespace Confuser.Core {
	/// <summary>
	///     Core component of Confuser.
	/// </summary>
	public class CoreComponent : ConfuserComponent {
		/// <summary>
		///     The service ID of RNG
		/// </summary>
		public const string _RandomServiceId = "Confuser.Random";

		/// <summary>
		///     The service ID of Marker
		/// </summary>
		public const string _MarkerServiceId = "Confuser.Marker";

		/// <summary>
		///     The service ID of Trace
		/// </summary>
		public const string _TraceServiceId = "Confuser.Trace";

		/// <summary>
		///     The service ID of Runtime
		/// </summary>
		public const string _RuntimeServiceId = "Confuser.Runtime";

		/// <summary>
		///     The service ID of Compression
		/// </summary>
		public const string _CompressionServiceId = "Confuser.Compression";

		/// <summary>
		///     The service ID of API Store
		/// </summary>
		public const string _APIStoreId = "Confuser.APIStore";

		readonly Marker marker;
		readonly ConfuserParameters parameters;

		/// <summary>
		///     Initializes a new instance of the <see cref="CoreComponent" /> class.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="marker">The marker.</param>
		internal CoreComponent(ConfuserParameters parameters, Marker marker) {
			this.parameters = parameters;
			this.marker = marker;
		}

		/// <inheritdoc />
		public override string Name {
			get { return "Confuser Core"; }
		}

		/// <inheritdoc />
		public override string Description {
			get { return "Initialization of Confuser core services."; }
		}

		/// <inheritdoc />
		public override string Id {
			get { return "Confuser.Core"; }
		}

		/// <inheritdoc />
		public override string FullId {
			get { return "Confuser.Core"; }
		}

		/// <inheritdoc />
		protected internal override void Initialize(ConfuserContext context) {
			context.Registry.RegisterService(_RandomServiceId, typeof(IRandomService), new RandomService(parameters.Project.Seed));
			context.Registry.RegisterService(_MarkerServiceId, typeof(IMarkerService), new MarkerService(context, marker));
			context.Registry.RegisterService(_TraceServiceId, typeof(ITraceService), new TraceService(context));
			context.Registry.RegisterService(_RuntimeServiceId, typeof(IRuntimeService), new RuntimeService());
			context.Registry.RegisterService(_CompressionServiceId, typeof(ICompressionService), new CompressionService(context));
			context.Registry.RegisterService(_APIStoreId, typeof(IAPIStore), new APIStore(context));
		}

		/// <inheritdoc />
		protected internal override void PopulatePipeline(ProtectionPipeline pipeline) {
			//
		}
	}
}