using System;
using System.Collections.Generic;

namespace Confuser.Renamer.BAML {
	internal class BamlDocument : List<BamlRecord> {
		public string DocumentName { get; set; }

		public string Signature { get; set; }
		public BamlVersion ReaderVersion { get; set; }
		public BamlVersion UpdaterVersion { get; set; }
		public BamlVersion WriterVersion { get; set; }

		public struct BamlVersion {
			public ushort Major;
			public ushort Minor;
		}
	}
}