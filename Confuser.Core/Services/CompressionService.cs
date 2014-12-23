using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Confuser.Core.Helpers;
using dnlib.DotNet;
using SevenZip;
using SevenZip.Compression.LZMA;

namespace Confuser.Core.Services {
	internal class CompressionService : ICompressionService {
		static readonly object Decompressor = new object();
		readonly ConfuserContext context;

		/// <summary>
		///     Initializes a new instance of the <see cref="CompressionService" /> class.
		/// </summary>
		/// <param name="context">The working context.</param>
		public CompressionService(ConfuserContext context) {
			this.context = context;
		}

		/// <inheritdoc />
		public MethodDef TryGetRuntimeDecompressor(ModuleDef module, Action<IDnlibDef> init) {
			var decompressor = context.Annotations.Get<Tuple<MethodDef, List<IDnlibDef>>>(module, Decompressor);
			if (decompressor == null)
				return null;

			foreach (IDnlibDef member in decompressor.Item2)
				init(member);
			return decompressor.Item1;
		}

		/// <inheritdoc />
		public MethodDef GetRuntimeDecompressor(ModuleDef module, Action<IDnlibDef> init) {
			Tuple<MethodDef, List<IDnlibDef>> decompressor = context.Annotations.GetOrCreate(module, Decompressor, m => {
				var rt = context.Registry.GetService<IRuntimeService>();

				List<IDnlibDef> members = InjectHelper.Inject(rt.GetRuntimeType("Confuser.Runtime.Lzma"), module.GlobalType, module).ToList();
				MethodDef decomp = null;
				foreach (IDnlibDef member in members) {
					if (member is MethodDef) {
						var method = (MethodDef)member;
						if (method.Access == MethodAttributes.Public)
							method.Access = MethodAttributes.Assembly;
						if (!method.IsConstructor)
							method.IsSpecialName = false;

						if (method.Name == "Decompress")
							decomp = method;
					}
					else if (member is FieldDef) {
						var field = (FieldDef)member;
						if (field.Access == FieldAttributes.Public)
							field.Access = FieldAttributes.Assembly;
						if (field.IsLiteral) {
							field.DeclaringType.Fields.Remove(field);
						}
					}
				}
				members.RemoveWhere(def => def is FieldDef && ((FieldDef)def).IsLiteral);

				Debug.Assert(decomp != null);
				return Tuple.Create(decomp, members);
			});
			foreach (IDnlibDef member in decompressor.Item2)
				init(member);
			return decompressor.Item1;
		}

		/// <inheritdoc />
		public byte[] Compress(byte[] data, Action<double> progressFunc = null) {
			CoderPropID[] propIDs = {
				CoderPropID.DictionarySize,
				CoderPropID.PosStateBits,
				CoderPropID.LitContextBits,
				CoderPropID.LitPosBits,
				CoderPropID.Algorithm,
				CoderPropID.NumFastBytes,
				CoderPropID.MatchFinder,
				CoderPropID.EndMarker
			};
			object[] properties = {
				1 << 23,
				2,
				3,
				0,
				2,
				128,
				"bt4",
				false
			};

			var x = new MemoryStream();
			var encoder = new Encoder();
			encoder.SetCoderProperties(propIDs, properties);
			encoder.WriteCoderProperties(x);
			Int64 fileSize;
			fileSize = data.Length;
			for (int i = 0; i < 8; i++)
				x.WriteByte((Byte)(fileSize >> (8 * i)));

			ICodeProgress progress = null;
			if (progressFunc != null)
				progress = new CompressionLogger(progressFunc, data.Length);
			encoder.Code(new MemoryStream(data), x, -1, -1, progress);

			return x.ToArray();
		}

		class CompressionLogger : ICodeProgress {
			readonly Action<double> progressFunc;
			readonly int size;

			public CompressionLogger(Action<double> progressFunc, int size) {
				this.progressFunc = progressFunc;
				this.size = size;
			}

			public void SetProgress(long inSize, long outSize) {
				double precentage = (double)inSize / size;
				progressFunc(precentage);
			}
		}
	}

	/// <summary>
	///     Provides methods to do compression and inject decompression algorithm.
	/// </summary>
	public interface ICompressionService {
		/// <summary>
		///     Gets the runtime decompression method in the module, or null if it's not yet injected.
		/// </summary>
		/// <param name="module">The module which the decompression method resides in.</param>
		/// <param name="init">The initializing method for compression helper definitions.</param>
		/// <returns>
		///     The requested decompression method with signature 'static Byte[] (Byte[])',
		///     or null if it hasn't been injected yet.
		/// </returns>
		MethodDef TryGetRuntimeDecompressor(ModuleDef module, Action<IDnlibDef> init);

		/// <summary>
		///     Gets the runtime decompression method in the module and inject if it does not exists.
		/// </summary>
		/// <param name="module">The module which the decompression method resides in.</param>
		/// <param name="init">The initializing method for injected helper definitions.</param>
		/// <returns>The requested decompression method with signature 'static Byte[] (Byte[])'.</returns>
		MethodDef GetRuntimeDecompressor(ModuleDef module, Action<IDnlibDef> init);

		/// <summary>
		///     Compresses the specified data.
		/// </summary>
		/// <param name="data">The buffer storing the data.</param>
		/// <param name="progressFunc">The function that receive the progress of compression.</param>
		/// <returns>The compressed data.</returns>
		byte[] Compress(byte[] data, Action<double> progressFunc = null);
	}
}