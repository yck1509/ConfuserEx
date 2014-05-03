using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core.Helpers;
using System.Diagnostics;
using System.IO;
using SevenZip;

namespace Confuser.Core.Services
{
    class CompressionService : ICompressionService
    {
        ConfuserContext context;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionService"/> class.
        /// </summary>
        /// <param name="context">The working context.</param>
        public CompressionService(ConfuserContext context)
        {
            this.context = context;
        }

        static readonly object Decompressor = new object();

        /// <inheritdoc/>
        public MethodDef GetRuntimeDecompressor(ModuleDef module, Action<IDefinition> init)
        {
            var decompressor = context.Annotations.GetOrCreate(module, Decompressor, m =>
            {
                IRuntimeService rt = context.Registry.GetService<IRuntimeService>();

                var members = InjectHelper.Inject(rt.GetRuntimeType("Confuser.Runtime.Lzma"), module.GlobalType, module).ToList();
                MethodDef decomp = null;
                foreach (var member in members)
                {
                    if (member is MethodDef)
                    {
                        MethodDef method = (MethodDef)member;
                        if (method.Access == MethodAttributes.Public)
                            method.Access = MethodAttributes.Assembly;
                        if (!method.IsConstructor)
                            method.IsSpecialName = false;

                        if (method.Name == "Decompress")
                            decomp = method;
                    }
                    else if (member is FieldDef)
                    {
                        FieldDef field = (FieldDef)member;
                        if (field.Access == FieldAttributes.Public)
                            field.Access = FieldAttributes.Assembly;
                        if (field.IsLiteral)
                        {
                            field.DeclaringType.Fields.Remove(field);
                            continue;
                        }
                    }
                }
                members.RemoveWhere(def => def is FieldDef && ((FieldDef)def).IsLiteral);

                Debug.Assert(decomp != null);
                return Tuple.Create(decomp, members);
            });
            foreach (var member in decompressor.Item2)
                init(member);
            return decompressor.Item1;
        }

        /// <inheritdoc/>
        public byte[] Compress(byte[] data)
        {
            CoderPropID[] propIDs = 
			{
				CoderPropID.DictionarySize,
				CoderPropID.PosStateBits,
				CoderPropID.LitContextBits,
				CoderPropID.LitPosBits,
				CoderPropID.Algorithm,
				CoderPropID.NumFastBytes,
				CoderPropID.MatchFinder,
				CoderPropID.EndMarker
			};
            object[] properties = 
			{
				(int)(1 << 23),
				2,
				3,
				0,
				2,
				128,
				"bt4",
				false
			};

            MemoryStream x = new MemoryStream();
            var encoder = new SevenZip.Compression.LZMA.Encoder();
            encoder.SetCoderProperties(propIDs, properties);
            encoder.WriteCoderProperties(x);
            Int64 fileSize;
            fileSize = data.Length;
            for (int i = 0; i < 8; i++)
                x.WriteByte((Byte)(fileSize >> (8 * i)));
            encoder.Code(new MemoryStream(data), x, -1, -1, null);
            return x.ToArray();
        }
    }

    /// <summary>
    /// Provides methods to do compression and inject decompression algorithm.
    /// </summary>
    public interface ICompressionService
    {
        /// <summary>
        /// Gets the runtime decompression method in the module and inject if it does not exists.
        /// </summary>
        /// <param name="module">The module which the decompression method resides in.</param>
        /// <param name="init">The initializing method for injected helper definitions.</param>
        /// <returns>The requested decompression method with signature 'static Byte[] (Byte[])'.</returns>
        MethodDef GetRuntimeDecompressor(ModuleDef module, Action<IDefinition> init);

        /// <summary>
        /// Compresses the specified data.
        /// </summary>
        /// <param name="data">The buffer storing the data.</param>
        /// <returns>The compressed data.</returns>
        byte[] Compress(byte[] data);
    }
}
