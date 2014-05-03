using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Confuser.Core;
using Confuser.Core.Services;
using Confuser.Renamer.Analyzers;

namespace Confuser.Renamer
{
    public interface INameService
    {
        VTableStorage GetVTables();

        void Analyze(IDefinition def);

        bool CanRename(object obj);
        void SetCanRename(object obj, bool val);

        RenameMode GetRenameMode(object obj);
        void SetRenameMode(object obj, RenameMode val);
        void ReduceRenameMode(object obj, RenameMode val);

        string ObfuscateName(string name, RenameMode mode);
        string RandomName();
        string RandomName(RenameMode mode);

        void RegisterRenamer(IRenamer renamer);
        void AddReference<T>(T obj, INameReference<T> reference);

        void SetOriginalName(object obj, string name);
        void SetOriginalNamespace(object obj, string ns);

        void MarkHelper(IDefinition def, IMarkerService marker);
    }

    class NameService : INameService
    {
        ConfuserContext context;
        public NameService(ConfuserContext context)
        {
            this.context = context;
            this.random = context.Registry.GetService<IRandomService>().GetRandomGenerator(NameProtection._FullId);
            this.nameSeed = random.NextBytes(20);

            this.Renamers = new List<IRenamer>()
            {
                new InterReferenceAnalyzer(),
                new VTableAnalyzer(),
                new TypeBlobAnalyzer(),
                new ResourceAnalyzer(),
                new WPFAnalyzer(),
                new LdtokenEnumAnalyzer()
            };
        }

        byte[] nameSeed;
        RandomGenerator random;
        public RandomGenerator GetRandom()
        {
            return random;
        }

        VTableStorage storage = new VTableStorage();
        public VTableStorage GetVTables()
        {
            return storage;
        }

        static readonly object CanRenameKey = new object();
        public bool CanRename(object obj)
        {
            return context.Annotations.Get<bool>(obj, CanRenameKey, true);
        }
        public void SetCanRename(object obj, bool val)
        {
            context.Annotations.Set<bool>(obj, CanRenameKey, val);
        }

        static readonly object RenameModeKey = new object();
        public RenameMode GetRenameMode(object obj)
        {
            return context.Annotations.Get<RenameMode>(obj, RenameModeKey, RenameMode.Unicode);
        }
        public void SetRenameMode(object obj, RenameMode val)
        {
            context.Annotations.Set<RenameMode>(obj, RenameModeKey, val);
        }
        public void ReduceRenameMode(object obj, RenameMode val)
        {
            RenameMode original = GetRenameMode(obj);
            if (original < val)
                context.Annotations.Set<RenameMode>(obj, RenameModeKey, val);
        }

        static readonly object ReferencesKey = new object();
        public void AddReference<T>(T obj, INameReference<T> reference)
        {
            context.Annotations.GetOrCreate<List<INameReference>>(obj, ReferencesKey, key => new List<INameReference>()).Add(reference);
        }
        public IList<INameReference> GetReferences(object obj)
        {
            return context.Annotations.GetLazy<List<INameReference>>(obj, ReferencesKey, key => new List<INameReference>());
        }

        AnalyzePhase analyze;
        public void Analyze(IDefinition def)
        {
            if (analyze == null)
                analyze = context.Pipeline.FindPhase<AnalyzePhase>();

            SetOriginalName(def, def.Name);
            if (def is TypeDef)
            {
                GetVTables().GetVTable((TypeDef)def);
                SetOriginalNamespace(def, ((TypeDef)def).Namespace);
            }
            analyze.Analyze(this, context, def, true);
        }

        #region Charsets
        static char[] asciiCharset = Enumerable.Range(32, 95)
            .Select(ord => (char)ord)
            .Except(new[] { '.' })
            .ToArray();
        static char[] letterCharset = Enumerable.Range(0, 26)
            .SelectMany(ord => new[] { (char)('a' + ord), (char)('A' + ord) })
            .ToArray();
        // Especially chosen, just to mess with people.
        // Inspired by: http://xkcd.com/1137/ :D
        static char[] unicodeCharset = new char[] { }
            .Concat(Enumerable.Range(0x200b, 5).Select(ord => (char)ord))
            .Concat(Enumerable.Range(0x2028, 7).Select(ord => (char)ord))
            .Concat(Enumerable.Range(0x206a, 6).Select(ord => (char)ord))
            .Except(new[] { '\u2029' })
            .ToArray();
        #endregion

        public string ObfuscateName(string name, RenameMode mode)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            if (mode == RenameMode.Empty)
                return "";
            else if (mode == RenameMode.Debug)
                return "_" + name;

            byte[] hash = Utils.Xor(Utils.SHA1(Encoding.UTF8.GetBytes(name)), nameSeed);

            switch (mode)
            {
                case RenameMode.Empty:
                    return "";
                case RenameMode.Unicode:
                    return Utils.EncodeString(hash, unicodeCharset) + "\u202e";
                case RenameMode.Letters:
                    return Utils.EncodeString(hash, letterCharset);
                case RenameMode.ASCII:
                    return Utils.EncodeString(hash, asciiCharset);
            }
            throw new NotSupportedException("Rename mode '" + mode.ToString() + "' is not supported.");
        }
        public string RandomName()
        {
            return RandomName(RenameMode.Unicode);
        }
        public string RandomName(RenameMode mode)
        {
            return ObfuscateName(Utils.ToHexString(random.NextBytes(16)), mode);
        }

        static readonly object OriginalNameKey = new object();
        public string GetOriginalName(object obj)
        {
            return context.Annotations.Get<string>(obj, OriginalNameKey, "");
        }
        public void SetOriginalName(object obj, string name)
        {
            context.Annotations.Set<string>(obj, OriginalNameKey, name);
        }

        static readonly object OriginalNamespaceKey = new object();
        public string GetOriginalNamespace(object obj)
        {
            return context.Annotations.Get<string>(obj, OriginalNamespaceKey, "");
        }
        public void SetOriginalNamespace(object obj, string ns)
        {
            context.Annotations.Set<string>(obj, OriginalNamespaceKey, ns);
        }


        public IList<IRenamer> Renamers { get; private set; }
        public void RegisterRenamer(IRenamer renamer)
        {
            Renamers.Add(renamer);
        }


        public void MarkHelper(IDefinition def, IMarkerService marker)
        {
            if (marker.IsMarked(def))
                return;
            if (def is MethodDef)
            {
                MethodDef method = (MethodDef)def;
                method.Access = MethodAttributes.Assembly;
                if (!method.IsSpecialName && !method.IsRuntimeSpecialName)
                    method.Name = RandomName();
            }
            else if (def is FieldDef)
            {
                FieldDef field = (FieldDef)def;
                field.Access = FieldAttributes.Assembly;
                field.Name = RandomName();
                if (!field.IsSpecialName && !field.IsRuntimeSpecialName)
                    field.Name = RandomName();
            }
            else if (def is TypeDef)
            {
                TypeDef type = (TypeDef)def;
                type.Visibility = type.DeclaringType == null ? TypeAttributes.NotPublic : TypeAttributes.NestedAssembly;
                type.Namespace = "";
                if (!type.IsSpecialName && !type.IsRuntimeSpecialName)
                    type.Name = RandomName();
            }
            SetCanRename(def, false);
            Analyze(def);
            marker.Mark(def);
        }
    }
}
