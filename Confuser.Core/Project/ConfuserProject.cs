using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using dnlib.DotNet;

namespace Confuser.Core.Project {
	/// <summary>
	///     A module description in a Confuser project.
	/// </summary>
	public class ProjectModule {
		/// <summary>
		///     Initializes a new instance of the <see cref="ProjectModule" /> class.
		/// </summary>
		public ProjectModule() {
			Rules = new List<Rule>();
		}

		/// <summary>
		///     Gets the path to the module.
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		///     Indicates whether this module is external and should not be obfuscated.
		/// </summary>
		public bool IsExternal { get; set; }

		/// <summary>
		///     Gets or sets the path to the strong name private key for signing.
		/// </summary>
		/// <value>The path to the strong name private key, or null if not necessary.</value>
		public string SNKeyPath { get; set; }

		/// <summary>
		///     Gets or sets the password of the strong name private key.
		/// </summary>
		/// <value>The password of the strong name private key, or null if not necessary.</value>
		public string SNKeyPassword { get; set; }

		/// <summary>
		///     Gets a list of protection rules applies to the module.
		/// </summary>
		/// <value>A list of protection rules.</value>
		public IList<Rule> Rules { get; private set; }

		/// <summary>
		///     Resolves the module from the path.
		/// </summary>
		/// <param name="basePath">
		///     The base path for the relative module path,
		///     or null if the module path is absolute or relative to current directory.
		/// </param>
		/// <param name="context">The resolved module's context.</param>
		/// <returns>The resolved module.</returns>
		public ModuleDefMD Resolve(string basePath, ModuleContext context = null) {
			if (basePath == null)
				return ModuleDefMD.Load(Path, context);
			return ModuleDefMD.Load(System.IO.Path.Combine(basePath, Path), context);
		}

		/// <summary>
		///     Read the raw bytes of the module from the path.
		/// </summary>
		/// <param name="basePath">
		///     The base path for the relative module path,
		///     or null if the module path is absolute or relative to current directory.
		/// </param>
		/// <returns>The loaded module.</returns>
		public byte[] LoadRaw(string basePath) {
			if (basePath == null)
				return File.ReadAllBytes(Path);
			return File.ReadAllBytes(System.IO.Path.Combine(basePath, Path));
		}

		/// <summary>
		///     Saves the module description as XML element.
		/// </summary>
		/// <param name="xmlDoc">The root XML document.</param>
		/// <returns>The serialized module description.</returns>
		internal XmlElement Save(XmlDocument xmlDoc) {
			XmlElement elem = xmlDoc.CreateElement("module", ConfuserProject.Namespace);

			XmlAttribute nameAttr = xmlDoc.CreateAttribute("path");
			nameAttr.Value = Path;
			elem.Attributes.Append(nameAttr);

			if (IsExternal) {
				XmlAttribute extAttr = xmlDoc.CreateAttribute("external");
				extAttr.Value = IsExternal.ToString();
				elem.Attributes.Append(extAttr);
			}
			if (SNKeyPath != null) {
				XmlAttribute snKeyAttr = xmlDoc.CreateAttribute("snKey");
				snKeyAttr.Value = SNKeyPath;
				elem.Attributes.Append(snKeyAttr);
			}
			if (SNKeyPassword != null) {
				XmlAttribute snKeyPassAttr = xmlDoc.CreateAttribute("snKeyPass");
				snKeyPassAttr.Value = SNKeyPassword;
				elem.Attributes.Append(snKeyPassAttr);
			}


			foreach (Rule i in Rules)
				elem.AppendChild(i.Save(xmlDoc));

			return elem;
		}

		/// <summary>
		///     Loads the module description from XML element.
		/// </summary>
		/// <param name="elem">The serialized module description.</param>
		internal void Load(XmlElement elem) {
			Path = elem.Attributes["path"].Value;

			if (elem.Attributes["external"] != null)
				IsExternal = bool.Parse(elem.Attributes["external"].Value);
			else
				IsExternal = false;

			if (elem.Attributes["snKey"] != null)
				SNKeyPath = elem.Attributes["snKey"].Value.NullIfEmpty();
			else
				SNKeyPath = null;

			if (elem.Attributes["snKeyPass"] != null)
				SNKeyPassword = elem.Attributes["snKeyPass"].Value.NullIfEmpty();
			else
				SNKeyPassword = null;

			Rules.Clear();
			foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>()) {
				var rule = new Rule();
				rule.Load(i);
				Rules.Add(rule);
			}
		}

		/// <summary>
		///     Returns a <see cref="string" /> that represents this instance.
		/// </summary>
		/// <returns>A <see cref="string" /> that represents this instance.</returns>
		public override string ToString() {
			return Path;
		}

		/// <summary>
		///     Clones this instance.
		/// </summary>
		/// <returns>A duplicated module.</returns>
		public ProjectModule Clone() {
			var ret = new ProjectModule();
			ret.Path = Path;
			ret.IsExternal = IsExternal;
			ret.SNKeyPath = SNKeyPath;
			ret.SNKeyPassword = SNKeyPassword;
			foreach (var r in Rules)
				ret.Rules.Add(r.Clone());
			return ret;
		}
	}

	/// <summary>
	///     Indicates add or remove the protection from the active protections
	/// </summary>
	public enum SettingItemAction {
		/// <summary>
		///     Add the protection to the active protections
		/// </summary>
		Add,

		/// <summary>
		///     Remove the protection from the active protections
		/// </summary>
		Remove
	}

	/// <summary>
	///     A <see cref="ConfuserComponent" /> setting within a rule.
	/// </summary>
	/// <typeparam name="T"><see cref="Protection" /> or <see cref="Packer" /></typeparam>
	public class SettingItem<T> : Dictionary<string, string> {
		/// <summary>
		/// Initialize this setting item instance
		/// </summary>
		/// <param name="id">The protection id</param>
		/// <param name="action">The action to take</param>
		public SettingItem(string id = null, SettingItemAction action = SettingItemAction.Add) {
			Id = id;
			Action = action;
		}

		/// <summary>
		///     The identifier of component
		/// </summary>
		/// <value>The identifier of component.</value>
		/// <seealso cref="ConfuserComponent.Id" />
		public string Id { get; set; }

		/// <summary>
		///     Gets or sets the action of component.
		/// </summary>
		/// <value>The action of component.</value>
		public SettingItemAction Action { get; set; }

		/// <summary>
		///     Saves the setting description as XML element.
		/// </summary>
		/// <param name="xmlDoc">The root XML document.</param>
		/// <returns>The setting module description.</returns>
		internal XmlElement Save(XmlDocument xmlDoc) {
			XmlElement elem = xmlDoc.CreateElement(typeof(T) == typeof(Packer) ? "packer" : "protection", ConfuserProject.Namespace);

			XmlAttribute idAttr = xmlDoc.CreateAttribute("id");
			idAttr.Value = Id;
			elem.Attributes.Append(idAttr);

			if (Action != SettingItemAction.Add) {
				XmlAttribute pAttr = xmlDoc.CreateAttribute("action");
				pAttr.Value = Action.ToString().ToLower();
				elem.Attributes.Append(pAttr);
			}

			foreach (var i in this) {
				XmlElement arg = xmlDoc.CreateElement("argument", ConfuserProject.Namespace);

				XmlAttribute nameAttr = xmlDoc.CreateAttribute("name");
				nameAttr.Value = i.Key;
				arg.Attributes.Append(nameAttr);
				XmlAttribute valAttr = xmlDoc.CreateAttribute("value");
				valAttr.Value = i.Value;
				arg.Attributes.Append(valAttr);

				elem.AppendChild(arg);
			}

			return elem;
		}

		/// <summary>
		///     Loads the setting description from XML element.
		/// </summary>
		/// <param name="elem">The serialized setting description.</param>
		internal void Load(XmlElement elem) {
			Id = elem.Attributes["id"].Value;

			if (elem.Attributes["action"] != null)
				Action = (SettingItemAction)Enum.Parse(typeof(SettingItemAction), elem.Attributes["action"].Value, true);
			else
				Action = SettingItemAction.Add;

			Clear();
			foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
				Add(i.Attributes["name"].Value, i.Attributes["value"].Value);
		}

		/// <summary>
		///     Clones this instance.
		/// </summary>
		/// <returns>A duplicated setting item.</returns>
		public SettingItem<T> Clone() {
			var item = new SettingItem<T>(Id, Action);
			foreach (var entry in this)
				item.Add(entry.Key, entry.Value);
			return item;
		}
	}


	/// <summary>
	///     A rule that control how <see cref="Protection" />s are applied to module
	/// </summary>
	public class Rule : List<SettingItem<Protection>> {
		/// <summary>
		/// Initialize this rule instance
		/// </summary>
		/// <param name="pattern">The pattern</param>
		/// <param name="preset">The preset</param>
		/// <param name="inherit">Inherits protection</param>
		public Rule(string pattern = "true", ProtectionPreset preset = ProtectionPreset.None, bool inherit = false) {
			Pattern = pattern;
			Preset = preset;
			Inherit = inherit;
		}

		/// <summary>
		///     Gets or sets the pattern that determine the target components of the rule.
		/// </summary>
		/// <value>The pattern expression.</value>
		public string Pattern { get; set; }

		/// <summary>
		///     Gets or sets the protection preset this rule uses.
		/// </summary>
		/// <value>The protection preset.</value>
		public ProtectionPreset Preset { get; set; }

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="Rule" /> inherits settings from earlier rules.
		/// </summary>
		/// <value><c>true</c> if it inherits settings; otherwise, <c>false</c>.</value>
		public bool Inherit { get; set; }

		/// <summary>
		///     Saves the rule description as XML element.
		/// </summary>
		/// <param name="xmlDoc">The root XML document.</param>
		/// <returns>The serialized rule description.</returns>
		internal XmlElement Save(XmlDocument xmlDoc) {
			XmlElement elem = xmlDoc.CreateElement("rule", ConfuserProject.Namespace);

			XmlAttribute ruleAttr = xmlDoc.CreateAttribute("pattern");
			ruleAttr.Value = Pattern;
			elem.Attributes.Append(ruleAttr);

			if (Preset != ProtectionPreset.None) {
				XmlAttribute pAttr = xmlDoc.CreateAttribute("preset");
				pAttr.Value = Preset.ToString().ToLower();
				elem.Attributes.Append(pAttr);
			}

			if (Inherit != true) {
				XmlAttribute attr = xmlDoc.CreateAttribute("inherit");
				attr.Value = Inherit.ToString().ToLower();
				elem.Attributes.Append(attr);
			}

			foreach (var i in this)
				elem.AppendChild(i.Save(xmlDoc));

			return elem;
		}

		/// <summary>
		///     Loads the rule description from XML element.
		/// </summary>
		/// <param name="elem">The serialized module description.</param>
		internal void Load(XmlElement elem) {
			Pattern = elem.Attributes["pattern"].Value;

			if (elem.Attributes["preset"] != null)
				Preset = (ProtectionPreset)Enum.Parse(typeof(ProtectionPreset), elem.Attributes["preset"].Value, true);
			else
				Preset = ProtectionPreset.None;

			if (elem.Attributes["inherit"] != null)
				Inherit = bool.Parse(elem.Attributes["inherit"].Value);
			else
				Inherit = true;

			Clear();
			foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>()) {
				var x = new SettingItem<Protection>();
				x.Load(i);
				Add(x);
			}
		}


		/// <summary>
		///     Clones this instance.
		/// </summary>
		/// <returns>A duplicated rule.</returns>
		public Rule Clone() {
			var ret = new Rule();
			ret.Preset = Preset;
			ret.Pattern = Pattern;
			ret.Inherit = Inherit;
			foreach (var i in this) {
				var item = new SettingItem<Protection>();
				item.Id = i.Id;
				item.Action = i.Action;
				foreach (string j in i.Keys)
					item.Add(j, i[j]);
				ret.Add(item);
			}
			return ret;
		}
	}

	/// <summary>
	///     The exception that is thrown when there exists schema errors in the project XML.
	/// </summary>
	public class ProjectValidationException : Exception {
		/// <summary>
		///     Initializes a new instance of the <see cref="ProjectValidationException" /> class.
		/// </summary>
		/// <param name="exceptions">The list of schema exceptions.</param>
		internal ProjectValidationException(List<XmlSchemaException> exceptions)
			: base(exceptions[0].Message) {
			Errors = exceptions;
		}

		/// <summary>
		///     Gets the schema exceptions.
		/// </summary>
		/// <value>A list of schema exceptions.</value>
		public IList<XmlSchemaException> Errors { get; private set; }
	}

	/// <summary>
	///     Represent a project of Confuser.
	/// </summary>
	public class ConfuserProject : List<ProjectModule> {
		/// <summary>
		///     The namespace of Confuser project schema
		/// </summary>
		public const string Namespace = "http://confuser.codeplex.com";

		/// <summary>
		///     The schema of project XML.
		/// </summary>
		public static readonly XmlSchema Schema = XmlSchema.Read(typeof(ConfuserProject).Assembly.GetManifestResourceStream("Confuser.Core.Project.ConfuserPrj.xsd"), null);

		/// <summary>
		///     Initializes a new instance of the <see cref="ConfuserProject" /> class.
		/// </summary>
		public ConfuserProject() {
			ProbePaths = new List<string>();
			PluginPaths = new List<string>();
			Rules = new List<Rule>();
		}

		/// <summary>
		///     Gets or sets the seed of pseudo-random generator used in process of protection.
		/// </summary>
		/// <value>The random seed.</value>
		public string Seed { get; set; }

		/// <summary>
		///     Gets or sets a value indicating whether debug symbols are generated.
		/// </summary>
		/// <value><c>true</c> if debug symbols are generated; otherwise, <c>false</c>.</value>
		public bool Debug { get; set; }

		/// <summary>
		///     Gets or sets the output directory.
		/// </summary>
		/// <value>The output directory.</value>
		public string OutputDirectory { get; set; }

		/// <summary>
		///     Gets or sets the base directory of the project.
		/// </summary>
		/// <value>The base directory.</value>
		public string BaseDirectory { get; set; }

		/// <summary>
		///     Gets a list of protection rules that applies globally.
		/// </summary>
		/// <value>A list of protection rules.</value>
		public IList<Rule> Rules { get; private set; }

		/// <summary>
		///     Gets or sets the packer used to pack up the output.
		/// </summary>
		/// <value>The packer.</value>
		public SettingItem<Packer> Packer { get; set; }

		/// <summary>
		///     Gets a list of paths that used to resolve assemblies.
		/// </summary>
		/// <value>The list of paths.</value>
		public IList<string> ProbePaths { get; private set; }

		/// <summary>
		///     Gets a list of paths to plugin.
		/// </summary>
		/// <value>The list of plugins.</value>
		public IList<string> PluginPaths { get; private set; }

		/// <summary>
		///     Saves the project as XML document.
		/// </summary>
		/// <returns>The serialized project XML.</returns>
		public XmlDocument Save() {
			var xmlDoc = new XmlDocument();
			xmlDoc.Schemas.Add(Schema);

			XmlElement elem = xmlDoc.CreateElement("project", Namespace);

			XmlAttribute outputAttr = xmlDoc.CreateAttribute("outputDir");
			outputAttr.Value = OutputDirectory;
			elem.Attributes.Append(outputAttr);

			XmlAttribute baseAttr = xmlDoc.CreateAttribute("baseDir");
			baseAttr.Value = BaseDirectory;
			elem.Attributes.Append(baseAttr);

			if (Seed != null) {
				XmlAttribute seedAttr = xmlDoc.CreateAttribute("seed");
				seedAttr.Value = Seed;
				elem.Attributes.Append(seedAttr);
			}

			if (Debug) {
				XmlAttribute debugAttr = xmlDoc.CreateAttribute("debug");
				debugAttr.Value = Debug.ToString().ToLower();
				elem.Attributes.Append(debugAttr);
			}

			foreach (Rule i in Rules)
				elem.AppendChild(i.Save(xmlDoc));

			if (Packer != null)
				elem.AppendChild(Packer.Save(xmlDoc));

			foreach (ProjectModule i in this)
				elem.AppendChild(i.Save(xmlDoc));

			foreach (string i in ProbePaths) {
				XmlElement path = xmlDoc.CreateElement("probePath", Namespace);
				path.InnerText = i;
				elem.AppendChild(path);
			}

			foreach (string i in PluginPaths) {
				XmlElement path = xmlDoc.CreateElement("plugin", Namespace);
				path.InnerText = i;
				elem.AppendChild(path);
			}

			xmlDoc.AppendChild(elem);
			return xmlDoc;
		}

		/// <summary>
		///     Loads the project from specified XML document.
		/// </summary>
		/// <param name="doc">The XML document storing the project.</param>
		/// <exception cref="Confuser.Core.Project.ProjectValidationException">
		///     The project XML contains schema errors.
		/// </exception>
		public void Load(XmlDocument doc) {
			doc.Schemas.Add(Schema);
			var exceptions = new List<XmlSchemaException>();
			doc.Validate((sender, e) => {
				if (e.Severity != XmlSeverityType.Error) return;
				exceptions.Add(e.Exception);
			});
			if (exceptions.Count > 0) {
				throw new ProjectValidationException(exceptions);
			}

			XmlElement docElem = doc.DocumentElement;

			OutputDirectory = docElem.Attributes["outputDir"].Value;
			BaseDirectory = docElem.Attributes["baseDir"].Value;

			if (docElem.Attributes["seed"] != null)
				Seed = docElem.Attributes["seed"].Value.NullIfEmpty();
			else
				Seed = null;

			if (docElem.Attributes["debug"] != null)
				Debug = bool.Parse(docElem.Attributes["debug"].Value);
			else
				Debug = false;

			Packer = null;
			Clear();
			ProbePaths.Clear();
			PluginPaths.Clear();
			Rules.Clear();
			foreach (XmlElement i in docElem.ChildNodes.OfType<XmlElement>()) {
				if (i.Name == "rule") {
					var rule = new Rule();
					rule.Load(i);
					Rules.Add(rule);
				}
				else if (i.Name == "packer") {
					Packer = new SettingItem<Packer>();
					Packer.Load(i);
				}
				else if (i.Name == "probePath") {
					ProbePaths.Add(i.InnerText);
				}
				else if (i.Name == "plugin") {
					PluginPaths.Add(i.InnerText);
				}
				else {
					var asm = new ProjectModule();
					asm.Load(i);
					Add(asm);
				}
			}
		}

		/// <summary>
		///     Clones this instance.
		/// </summary>
		/// <returns>A duplicated project.</returns>
		public ConfuserProject Clone() {
			var ret = new ConfuserProject();
			ret.Seed = Seed;
			ret.Debug = Debug;
			ret.OutputDirectory = OutputDirectory;
			ret.BaseDirectory = BaseDirectory;
			ret.Packer = Packer == null ? null : Packer.Clone();
			ret.ProbePaths = new List<string>(ProbePaths);
			ret.PluginPaths = new List<string>(PluginPaths);
			foreach (var module in this)
				ret.Add(module.Clone());
			foreach (var r in Rules)
				ret.Rules.Add(r);
			return ret;
		}
	}
}