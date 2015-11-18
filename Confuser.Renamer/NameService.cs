using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;
using dnlib.DotNet;

namespace Confuser.Renamer {
	public interface INameService {
		VTableStorage GetVTables();

		void Analyze(IDnlibDef def);

		bool CanRename(object obj);
		void SetCanRename(object obj, bool val);

		RenameMode GetRenameMode(object obj);
		void SetRenameMode(object obj, RenameMode val);
		void ReduceRenameMode(object obj, RenameMode val);

		string ObfuscateName(string name, RenameMode mode);
		string RandomName();
		string RandomName(RenameMode mode);

		void RegisterRenamer(IRenamer renamer);
		T FindRenamer<T>();
		void AddReference<T>(T obj, INameReference<T> reference);

		void SetOriginalName(object obj, string name);
		void SetOriginalNamespace(object obj, string ns);

		void MarkHelper(IDnlibDef def, IMarkerService marker, ConfuserComponent parentComp);
	}

	internal class NameService : INameService {
		static readonly object CanRenameKey = new object();
		static readonly object RenameModeKey = new object();
		static readonly object ReferencesKey = new object();
		static readonly object OriginalNameKey = new object();
		static readonly object OriginalNamespaceKey = new object();

		readonly ConfuserContext context;
		readonly byte[] nameSeed;
		readonly RandomGenerator random;
		readonly VTableStorage storage;
		AnalyzePhase analyze;
		readonly byte[] nameId = new byte[8];
		readonly Dictionary<string, string> nameMap1 = new Dictionary<string, string>();
		readonly Dictionary<string, string> nameMap2 = new Dictionary<string, string>();
		internal ReversibleRenamer reversibleRenamer;

		public NameService(ConfuserContext context) {
			this.context = context;
			storage = new VTableStorage(context.Logger);
			random = context.Registry.GetService<IRandomService>().GetRandomGenerator(NameProtection._FullId);
			nameSeed = random.NextBytes(20);

			Renamers = new List<IRenamer> {
				new InterReferenceAnalyzer(),
				new VTableAnalyzer(),
				new TypeBlobAnalyzer(),
				new ResourceAnalyzer(),
				new LdtokenEnumAnalyzer()
			};
		}

		public IList<IRenamer> Renamers { get; private set; }

		public VTableStorage GetVTables() {
			return storage;
		}

		public bool CanRename(object obj) {
			if (obj is IDnlibDef) {
				if (analyze == null)
					analyze = context.Pipeline.FindPhase<AnalyzePhase>();

				var prot = (NameProtection)analyze.Parent;
				ProtectionSettings parameters = ProtectionParameters.GetParameters(context, (IDnlibDef)obj);
				if (parameters == null || !parameters.ContainsKey(prot))
					return false;
				return context.Annotations.Get(obj, CanRenameKey, true);
			}
			return false;
		}

		public void SetCanRename(object obj, bool val) {
			context.Annotations.Set(obj, CanRenameKey, val);
		}

		public RenameMode GetRenameMode(object obj) {
			return context.Annotations.Get(obj, RenameModeKey, RenameMode.Unicode);
		}

		public void SetRenameMode(object obj, RenameMode val) {
			context.Annotations.Set(obj, RenameModeKey, val);
		}

		public void ReduceRenameMode(object obj, RenameMode val) {
			RenameMode original = GetRenameMode(obj);
			if (original < val)
				context.Annotations.Set(obj, RenameModeKey, val);
		}

		public void AddReference<T>(T obj, INameReference<T> reference) {
			context.Annotations.GetOrCreate(obj, ReferencesKey, key => new List<INameReference>()).Add(reference);
		}

		public void Analyze(IDnlibDef def) {
			if (analyze == null)
				analyze = context.Pipeline.FindPhase<AnalyzePhase>();

			SetOriginalName(def, def.Name);
			if (def is TypeDef) {
				GetVTables().GetVTable((TypeDef)def);
				SetOriginalNamespace(def, ((TypeDef)def).Namespace);
			}
			analyze.Analyze(this, context, ProtectionParameters.Empty, def, true);
		}

		void IncrementNameId() {
			for (int i = nameId.Length - 1; i >= 0; i--) {
				nameId[i]++;
				if (nameId[i] != 0)
					break;
			}
		}

		public string ObfuscateName(string name, RenameMode mode) {
			if (string.IsNullOrEmpty(name))
				return string.Empty;

			if (mode == RenameMode.Empty)
				return "";
			if (mode == RenameMode.Debug)
				return "_" + name;

			byte[] hash = Utils.Xor(Utils.SHA1(Encoding.UTF8.GetBytes(name)), nameSeed);

			switch (mode) {
				case RenameMode.Empty:
					return "";
				case RenameMode.Unicode:
					return Utils.EncodeString(hash, unicodeCharset) + "\u202e";
				case RenameMode.Letters:
					return Utils.EncodeString(hash, letterCharset);
				case RenameMode.ASCII:
					return Utils.EncodeString(hash, asciiCharset);
				case RenameMode.Decodable: {
					if (nameMap1.ContainsKey(name))
						return nameMap1[name];
					IncrementNameId();
					var newName = Utils.EncodeString(hash, alphaNumCharset);
					nameMap2[newName] = name;
					nameMap1[name] = newName;
					return newName;
				}
				case RenameMode.Sequential: {
					if (nameMap1.ContainsKey(name))
						return nameMap1[name];
					IncrementNameId();
					var newName = Utils.EncodeString(nameId, alphaNumCharset);
					nameMap2[newName] = name;
					nameMap1[name] = newName;
					return newName;
				}
				case RenameMode.Reversible: {
					if (reversibleRenamer == null)
						throw new ArgumentException("Password not provided for reversible renaming.");
					var newName = reversibleRenamer.Encrypt(name);
					return newName;
				}
			}
			throw new NotSupportedException("Rename mode '" + mode + "' is not supported.");
		}

		public string RandomName() {
			return RandomName(RenameMode.Unicode);
		}

		public string RandomName(RenameMode mode) {
			return ObfuscateName(Utils.ToHexString(random.NextBytes(16)), mode);
		}

		public void SetOriginalName(object obj, string name) {
			context.Annotations.Set(obj, OriginalNameKey, name);
		}

		public void SetOriginalNamespace(object obj, string ns) {
			context.Annotations.Set(obj, OriginalNamespaceKey, ns);
		}

		public void RegisterRenamer(IRenamer renamer) {
			Renamers.Add(renamer);
		}

		public T FindRenamer<T>() {
			return Renamers.OfType<T>().Single();
		}

		public void MarkHelper(IDnlibDef def, IMarkerService marker, ConfuserComponent parentComp) {
			if (marker.IsMarked(def))
				return;
			if (def is MethodDef) {
				var method = (MethodDef)def;
				method.Access = MethodAttributes.Assembly;
				if (!method.IsSpecialName && !method.IsRuntimeSpecialName && !method.DeclaringType.IsDelegate())
					method.Name = RandomName();
			}
			else if (def is FieldDef) {
				var field = (FieldDef)def;
				field.Access = FieldAttributes.Assembly;
				if (!field.IsSpecialName && !field.IsRuntimeSpecialName)
					field.Name = RandomName();
			}
			else if (def is TypeDef) {
				var type = (TypeDef)def;
				type.Visibility = type.DeclaringType == null ? TypeAttributes.NotPublic : TypeAttributes.NestedAssembly;
				type.Namespace = "";
				if (!type.IsSpecialName && !type.IsRuntimeSpecialName)
					type.Name = RandomName();
			}
			SetCanRename(def, false);
			Analyze(def);
			marker.Mark(def, parentComp);
		}

		#region Charsets

		static readonly char[] asciiCharset = Enumerable.Range(32, 95)
		                                                .Select(ord => (char)ord)
		                                                .Except(new[] { '.' })
		                                                .ToArray();

		static readonly char[] letterCharset = Enumerable.Range(0, 26)
		                                                 .SelectMany(ord => new[] { (char)('a' + ord), (char)('A' + ord) })
		                                                 .ToArray();

		static readonly char[] alphaNumCharset = Enumerable.Range(0, 26)
		                                                   .SelectMany(ord => new[] { (char)('a' + ord), (char)('A' + ord) })
		                                                   .Concat(Enumerable.Range(0, 10).Select(ord => (char)('0' + ord)))
		                                                   .ToArray();

		// Especially chosen, just to mess with people.
		// Inspired by: http://xkcd.com/1137/ :D
		static readonly char[] unicodeCharset = new char[] { }
			.Concat(Enumerable.Range(0x200b, 5).Select(ord => (char)ord))
			.Concat(Enumerable.Range(0x2029, 6).Select(ord => (char)ord))
			.Concat(Enumerable.Range(0x206a, 6).Select(ord => (char)ord))
			.Except(new[] { '\u2029' })
			.ToArray();

		#endregion

		public RandomGenerator GetRandom() {
			return random;
		}

		public IList<INameReference> GetReferences(object obj) {
			return context.Annotations.GetLazy(obj, ReferencesKey, key => new List<INameReference>());
		}

		public string GetOriginalName(object obj) {
			return context.Annotations.Get(obj, OriginalNameKey, "");
		}

		public string GetOriginalNamespace(object obj) {
			return context.Annotations.Get(obj, OriginalNamespaceKey, "");
		}

		public ICollection<KeyValuePair<string, string>> GetNameMap() {
			return nameMap2;
		}
	}
}