using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core.Project;
using System.Text.RegularExpressions;
using dnlib.DotNet;
using System.IO;

namespace Confuser.Core
{
    using Rules = Dictionary<Rule, Regex>;

    /// <summary>
    /// Resolves and marks the modules with protection settings according to the rules.
    /// </summary>
    public class Marker
    {
        Dictionary<string, Protection> protections;
        Dictionary<string, Packer> packers;

        /// <summary>
        /// Initalizes the Marker with specified protections and packers.
        /// </summary>
        /// <param name="protections">The protections.</param>
        /// <param name="packers">The packers.</param>
        public virtual void Initalize(IList<Protection> protections, IList<Packer> packers)
        {
            this.protections = protections.ToDictionary(prot => prot.Id, prot => prot, StringComparer.OrdinalIgnoreCase);
            this.packers = packers.ToDictionary(packer => packer.Id, packer => packer, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Fills the protection settings with the specified preset.
        /// </summary>
        /// <param name="preset">The preset.</param>
        /// <param name="settings">The settings.</param>
        void FillPreset(ProtectionPreset preset, ProtectionSettings settings)
        {
            foreach (Protection prot in protections.Values)
                if (prot.Preset <= preset && !settings.ContainsKey(prot))
                    settings.Add(prot, new Dictionary<string, string>());
        }

        /// <summary>
        /// Annotation key of Strong Name Key.
        /// </summary>
        public static readonly object SNKey = new object();

        /// <summary>
        /// Annotation key of rules.
        /// </summary>
        public static readonly object RulesKey = new object();

        /// <summary>
        /// Loads the Strong Name Key at the specified path with a optional password.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="path">The path to the key.</param>
        /// <param name="pass">The password of the certificate at <paramref name="path"/> if 
        /// it is a pfx file; otherwise, <c>null</c>.</param>
        /// <returns>The loaded Strong Name Key.</returns>
        static StrongNameKey LoadSNKey(ConfuserContext context, string path, string pass)
        {
            if (path == null) return null;

            try
            {
                if (pass != null)   //pfx
                {
                    // http://stackoverflow.com/a/12196742/462805
                    var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2();
                    cert.Import(path, pass, System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);

                    var rsa = cert.PrivateKey as System.Security.Cryptography.RSACryptoServiceProvider;
                    if (rsa == null)
                        throw new ArgumentException("RSA key does not present in the certificate.", "path");

                    return new StrongNameKey(rsa.ExportCspBlob(true));
                }
                else                //snk
                {
                    return new StrongNameKey(path);
                }
            }
            catch (Exception ex)
            {
                context.Logger.ErrorException("Cannot load the Strong Name Key located at: " + path, ex);
                throw new ConfuserException(ex);
            }
        }

        /// <summary>
        /// Loads the assembly and marks the project.
        /// </summary>
        /// <param name="proj">The project.</param>
        /// <param name="context">The working context.</param>
        /// <returns><see cref="MarkerResult"/> storing the marked modules and packer information.</returns>
        protected internal virtual MarkerResult MarkProject(ConfuserProject proj, ConfuserContext context)
        {
            Packer packer = null;
            Dictionary<string, string> packerParams = null;
            if (proj.Packer != null)
            {
                if (!packers.ContainsKey(proj.Packer.Id))
                {
                    context.Logger.ErrorFormat("Cannot find packer with ID '{0}'.", proj.Packer.Id);
                    throw new ConfuserException(null);
                }
                packer = packers[proj.Packer.Id];
                packerParams = new Dictionary<string, string>(proj.Packer);
            }

            List<ModuleDefMD> modules = new List<ModuleDefMD>();
            foreach (var module in proj)
            {
                context.Logger.InfoFormat("Loading '{0}'...", module.Path);
                ModuleDefMD modDef = module.Resolve(proj.BaseDirectory, context.Resolver.DefaultModuleContext);
                var rules = ParseRules(proj, module, context);

                context.Annotations.Set(modDef, SNKey, LoadSNKey(context, module.SNKeyPath, module.SNKeyPassword));
                context.Annotations.Set(modDef, RulesKey, rules);

                foreach (var def in modDef.FindDefinitions())
                    ApplyRules(context, def, rules);

                // Packer parameters are stored in modules
                if (packerParams != null)
                    ProtectionParameters.GetParameters(context, modDef)[packer] = packerParams;

                modules.Add(modDef);
            }
            return new MarkerResult(modules, packer);
        }

        /// <summary>
        /// Marks the member definition.
        /// </summary>
        /// <param name="member">The member definition.</param>
        /// <param name="context">The working context.</param>
        protected internal virtual void MarkMember(IDefinition member, ConfuserContext context)
        {
            ModuleDef module = ((IMemberRef)member).Module;
            var rules = context.Annotations.Get<Rules>(module, RulesKey);
            ApplyRules(context, member, rules);
        }

        /// <summary>
        /// Parses the rules' patterns.
        /// </summary>
        /// <param name="module">The module description.</param>
        /// <param name="context">The working context.</param>
        /// <returns>Parsed rule patterns.</returns>
        /// <exception cref="System.ArgumentException">
        /// One of the rules has invalid RegEx pattern.
        /// </exception>
        Rules ParseRules(ConfuserProject proj, ProjectModule module, ConfuserContext context)
        {
            var ret = new Rules();
            foreach (var rule in module.Rules.Concat(proj.Rules))
            {
                try
                {
                    Regex regex = new Regex(rule.Pattern);
                    ret.Add(rule, regex);
                }
                catch (Exception ex)
                {
                    context.Logger.ErrorFormat("Invalid rule pattern: " + rule.Pattern + ".", ex);
                    throw new ConfuserException(ex);
                }
                foreach (var setting in rule)
                {
                    if (!protections.ContainsKey(setting.Id))
                    {
                        context.Logger.ErrorFormat("Cannot find protection with ID '{0}'.", setting.Id);
                        throw new ConfuserException(null);
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Applies the rules to the target definition.
        /// </summary>
        /// <param name="context">The working context.</param>
        /// <param name="target">The target definition.</param>
        /// <param name="rules">The rules.</param>
        void ApplyRules(ConfuserContext context, IDefinition target, Rules rules)
        {
            ProtectionSettings ret = new ProtectionSettings();
            string sig = GetSignature(target);
            foreach (var i in rules)
            {
                if (!i.Value.IsMatch(sig)) continue;

                if (!i.Key.Inherit)
                    ret.Clear();

                FillPreset(i.Key.Preset, ret);
                foreach (var prot in i.Key)
                {
                    if (prot.Action == SettingItemAction.Add)
                        ret[protections[prot.Id]] = new Dictionary<string, string>(prot);
                    else
                        ret.Remove(protections[prot.Id]);
                }
            }

            ProtectionParameters.SetParameters(context, target, ret);
        }

        /// <summary>
        /// Gets the signature of a target definition.
        /// </summary>
        /// <param name="def">The target definition.</param>
        /// <returns>The signature of the definition.</returns>
        /// <exception cref="System.NotSupportedException">
        /// The definition is not supported.
        /// </exception>
        static string GetSignature(IDefinition def)
        {
            if (def is ModuleDef)
            {
                return ((ModuleDef)def).Name;
            }
            else if (def is TypeDef)
            {
                TypeDef type = (TypeDef)def;
                return type.Module.Name + "!!" + type.FullName;
            }
            else if (def is MethodDef)
            {
                MethodDef method = (MethodDef)def;
                return method.Module.Name + "!!" + method.FullName;
            }
            else if (def is FieldDef)
            {
                FieldDef field = (FieldDef)def;
                return field.Module.Name + "!!" + field.FullName;
            }
            else if (def is PropertyDef)
            {
                PropertyDef property = (PropertyDef)def;
                return property.Module.Name + "!!" + property.FullName;
            }
            else if (def is EventDef)
            {
                EventDef evt = (EventDef)def;
                return evt.Module.Name + "!!" + evt.FullName;
            }
            else
                throw new NotSupportedException();
        }
    }
}
