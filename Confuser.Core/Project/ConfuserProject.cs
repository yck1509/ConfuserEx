using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Collections.Specialized;
using System.Xml.Schema;
using dnlib.DotNet;

namespace Confuser.Core.Project
{
    /// <summary>
    /// A module description in a Confuser project.
    /// </summary>
    public class ProjectModule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectModule" /> class.
        /// </summary>
        public ProjectModule()
        {
            Rules = new List<Rule>();
        }

        /// <summary>
        /// Gets the path to the module.
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this module is the main module.
        /// </summary>
        /// <value><c>true</c> if this module is the main module; otherwise, <c>false</c>.</value>
        public bool IsMain { get; set; }
        /// <summary>
        /// Gets or sets the path to the strong name private key for signing.
        /// </summary>
        /// <value>The path to the strong name private key, or null if not necessary.</value>
        public string SNKeyPath { get; set; }
        /// <summary>
        /// Gets or sets the password of the strong name private key.
        /// </summary>
        /// <value>The password of the strong name private key, or null if not necessary.</value>
        public string SNKeyPassword { get; set; }
        /// <summary>
        /// Gets a list of protection rules applied to the module
        /// </summary>
        /// <value>A list of protection rules.</value>
        public IList<Rule> Rules { get; private set; }

        /// <summary>
        /// Resolves the module from the path.
        /// </summary>
        /// <param name="basePath">The base path for the relative module path,
        /// or null if the module path is absolute or relative to current directory.</param>
        /// <returns>The resolved module.</returns>
        public ModuleDef Resolve(string basePath)
        {
            if (basePath == null)
                return ModuleDefMD.Load(Path);
            else
                return ModuleDefMD.Load(System.IO.Path.Combine(basePath, Path));
        }

        /// <summary>
        /// Saves the module description as XML element.
        /// </summary>
        /// <param name="xmlDoc">The root XML document.</param>
        /// <returns>The serialized module description.</returns>
        internal XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("module", ConfuserProject.Namespace);

            XmlAttribute nameAttr = xmlDoc.CreateAttribute("path");
            nameAttr.Value = Path;
            elem.Attributes.Append(nameAttr);

            if (IsMain != false)
            {
                XmlAttribute mainAttr = xmlDoc.CreateAttribute("isMain");
                mainAttr.Value = IsMain.ToString().ToLower();
                elem.Attributes.Append(mainAttr);
            }
            if (SNKeyPath != null)
            {
                XmlAttribute snKeyAttr = xmlDoc.CreateAttribute("snKey");
                snKeyAttr.Value = SNKeyPath;
                elem.Attributes.Append(snKeyAttr);
            }
            if (SNKeyPassword != null)
            {
                XmlAttribute snKeyPassAttr = xmlDoc.CreateAttribute("snKeyPass");
                snKeyPassAttr.Value = SNKeyPassword;
                elem.Attributes.Append(snKeyPassAttr);
            }


            foreach (var i in Rules)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }

        /// <summary>
        /// Loads the module description from XML element.
        /// </summary>
        /// <param name="elem">The serialized module description.</param>
        internal void Load(XmlElement elem)
        {
            this.Path = elem.Attributes["path"].Value;
            if (elem.Attributes["isMain"] != null)
                this.IsMain = bool.Parse(elem.Attributes["isMain"].Value);
            if (elem.Attributes["snKey"] != null)
                this.SNKeyPath = elem.Attributes["snKey"].Value;
            if (elem.Attributes["snKeyPass"] != null)
                this.SNKeyPassword = elem.Attributes["snKeyPass"].Value;

            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                Rule settings = new Rule();
                settings.Load(i);
                Rules.Add(settings);
            }
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>A <see cref="string" /> that represents this instance.</returns>
        public override string ToString()
        {
            return Path;
        }
    }

    /// <summary>
    /// Indicates add or remove the protection from the active protections
    /// </summary>
    public enum SettingItemAction
    {
        /// <summary>
        /// Add the protection to the active protections
        /// </summary>
        Add,
        /// <summary>
        /// Remove the protection from the active protections
        /// </summary>
        Remove
    }

    /// <summary>
    /// A <see cref="Protection"/> setting within a rule.
    /// </summary>
    /// <typeparam name="T"><see cref="Protection"/> or <see cref="Packer"/></typeparam>
    public class SettingItem<T> : NameValueCollection
    {
        /// <summary>
        /// The identifier of protection
        /// </summary>
        /// <value>The identifier of protection.</value>
        /// <seealso cref="Protection.Id" />
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the action of protection.
        /// </summary>
        /// <value>The action of protection.</value>
        public SettingItemAction Action { get; set; }

        /// <summary>
        /// Saves the setting description as XML element.
        /// </summary>
        /// <param name="xmlDoc">The root XML document.</param>
        /// <returns>The setting module description.</returns>
        internal XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement(typeof(T) == typeof(Packer) ? "packer" : "protection", ConfuserProject.Namespace);

            XmlAttribute idAttr = xmlDoc.CreateAttribute("id");
            idAttr.Value = Id;
            elem.Attributes.Append(idAttr);

            if (Action != SettingItemAction.Add)
            {
                XmlAttribute pAttr = xmlDoc.CreateAttribute("action");
                pAttr.Value = Action.ToString().ToLower();
                elem.Attributes.Append(pAttr);
            }

            foreach (var i in this.AllKeys)
            {
                XmlElement arg = xmlDoc.CreateElement("argument", ConfuserProject.Namespace);

                XmlAttribute nameAttr = xmlDoc.CreateAttribute("name");
                nameAttr.Value = i;
                arg.Attributes.Append(nameAttr);
                XmlAttribute valAttr = xmlDoc.CreateAttribute("value");
                valAttr.Value = base[i];
                arg.Attributes.Append(valAttr);

                elem.AppendChild(arg);
            }

            return elem;
        }

        /// <summary>
        /// Loads the setting description from XML element.
        /// </summary>
        /// <param name="elem">The serialized setting description.</param>
        internal void Load(XmlElement elem)
        {
            this.Id = elem.Attributes["id"].Value;
            if (elem.Attributes["action"] != null)
                this.Action = (SettingItemAction)Enum.Parse(typeof(SettingItemAction), elem.Attributes["action"].Value, true);
            else
                this.Action = SettingItemAction.Add;
            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
                this.Add(i.Attributes["name"].Value, i.Attributes["value"].Value);
        }
    }


    /// <summary>
    /// A rule that control how <see cref="Protection"/>s are applied to module
    /// </summary>
    public class Rule : List<SettingItem<Protection>>
    {
        /// <summary>
        /// Gets or sets the Regular Expression pattern that determine the target components of the rule.
        /// </summary>
        /// <value>The RegEx pattern.</value>
        public string Pattern { get; set; }


        /// <summary>
        /// Gets or sets the protection preset this rule uses.
        /// </summary>
        /// <value>The protection preset.</value>
        public ProtectionPreset Preset { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Rule"/> inherits settings from earlier rules.
        /// </summary>
        /// <value><c>true</c> if it inherits settings; otherwise, <c>false</c>.</value>
        public bool Inherit { get; set; }

        /// <summary>
        /// Saves the rule description as XML element.
        /// </summary>
        /// <param name="xmlDoc">The root XML document.</param>
        /// <returns>The serialized rule description.</returns>
        internal XmlElement Save(XmlDocument xmlDoc)
        {
            XmlElement elem = xmlDoc.CreateElement("rule", ConfuserProject.Namespace);

            XmlAttribute ruleAttr = xmlDoc.CreateAttribute("pattern");
            ruleAttr.Value = Pattern;
            elem.Attributes.Append(ruleAttr);

            if (Preset != ProtectionPreset.None)
            {
                XmlAttribute pAttr = xmlDoc.CreateAttribute("preset");
                pAttr.Value = Preset.ToString().ToLower();
                elem.Attributes.Append(pAttr);
            }

            if (Inherit != true)
            {
                XmlAttribute attr = xmlDoc.CreateAttribute("inherit");
                attr.Value = Inherit.ToString().ToLower();
                elem.Attributes.Append(attr);
            }

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            return elem;
        }

        /// <summary>
        /// Loads the rule description from XML element.
        /// </summary>
        /// <param name="elem">The serialized module description.</param>
        internal void Load(XmlElement elem)
        {
            this.Pattern = elem.Attributes["pattern"].Value;

            if (elem.Attributes["preset"] != null)
                this.Preset = (ProtectionPreset)Enum.Parse(typeof(ProtectionPreset), elem.Attributes["preset"].Value, true);
            else
                this.Preset = ProtectionPreset.None;

            if (elem.Attributes["inherit"] != null)
                this.Inherit = bool.Parse(elem.Attributes["inherit"].Value);
            else
                this.Inherit = true;

            foreach (XmlElement i in elem.ChildNodes.OfType<XmlElement>())
            {
                var x = new SettingItem<Protection>();
                x.Load(i);
                this.Add(x);
            }
        }


        /// <summary>
        /// Clones this instance.
        /// </summary>
        /// <returns>A duplicated rule.</returns>
        public Rule Clone()
        {
            Rule ret = new Rule();
            ret.Preset = this.Preset;
            ret.Pattern = this.Pattern;
            ret.Inherit = this.Inherit;
            foreach (var i in this)
            {
                var item = new SettingItem<Protection>();
                item.Id = i.Id;
                item.Action = i.Action;
                foreach (var j in i.AllKeys)
                    item.Add(j, i[j]);
                ret.Add(item);
            }
            return ret;
        }
    }

    /// <summary>
    /// The exception that is thrown when there exists schema errors in the project XML.
    /// </summary>
    public class ProjectValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectValidationException"/> class.
        /// </summary>
        /// <param name="exceptions">The list of schema exceptions.</param>
        internal ProjectValidationException(List<XmlSchemaException> exceptions)
            : base(exceptions[0].Message)
        {
            Errors = exceptions;
        }

        /// <summary>
        /// Gets the schema exceptions.
        /// </summary>
        /// <value>A list of schema exceptions.</value>
        public IList<XmlSchemaException> Errors { get; private set; }
    }

    /// <summary>
    /// Represent a project of Confuser.
    /// </summary>
    public class ConfuserProject : List<ProjectModule>
    {
        /// <summary>
        /// Gets or sets the seed of pseudo-random generator used in process of protection.
        /// </summary>
        /// <value>The random seed.</value>
        public string Seed { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether debug symbols are generated.
        /// </summary>
        /// <value><c>true</c> if debug symbols are generated; otherwise, <c>false</c>.</value>
        public bool Debug { get; set; }

        /// <summary>
        /// Gets or sets the output directory.
        /// </summary>
        /// <value>The output directory.</value>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets the base path of the project.
        /// </summary>
        /// <value>The base path.</value>
        public string BasePath { get; set; }

        /// <summary>
        /// Gets or sets the packer used to pack up the output.
        /// </summary>
        /// <value>The packer.</value>
        public SettingItem<Packer> Packer { get; set; }

        /// <summary>
        /// The schema of project XML.
        /// </summary>
        public static readonly XmlSchema Schema = XmlSchema.Read(typeof(ConfuserProject).Assembly.GetManifestResourceStream("Confuser.Core.Project.ConfuserPrj.xsd"), null);

        /// <summary>
        /// The namespace of Confuser project schema
        /// </summary>
        public const string Namespace = "http://confuser.codeplex.com";

        /// <summary>
        /// Saves the project as XML document.
        /// </summary>
        /// <returns>The serialized project XML.</returns>
        public XmlDocument Save()
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Schemas.Add(Schema);

            XmlElement elem = xmlDoc.CreateElement("project", Namespace);

            XmlAttribute outputAttr = xmlDoc.CreateAttribute("outputDir");
            outputAttr.Value = OutputDirectory;
            elem.Attributes.Append(outputAttr);

            if (Seed != null)
            {
                XmlAttribute seedAttr = xmlDoc.CreateAttribute("seed");
                seedAttr.Value = Seed;
                elem.Attributes.Append(seedAttr);
            }

            if (Debug != false)
            {
                XmlAttribute debugAttr = xmlDoc.CreateAttribute("debug");
                debugAttr.Value = Debug.ToString().ToLower();
                elem.Attributes.Append(debugAttr);
            }

            if (Packer != null)
                elem.AppendChild(Packer.Save(xmlDoc));

            foreach (var i in this)
                elem.AppendChild(i.Save(xmlDoc));

            xmlDoc.AppendChild(elem);
            return xmlDoc;
        }

        /// <summary>
        /// Loads the project from specified XML document.
        /// </summary>
        /// <param name="doc">The XML document storing the project.</param>
        /// <exception cref="Confuser.Core.Project.ProjectValidationException">
        /// The project XML contains schema errors.
        /// </exception>
        public void Load(XmlDocument doc)
        {
            doc.Schemas.Add(Schema);
            List<XmlSchemaException> exceptions = new List<XmlSchemaException>();
            doc.Validate((sender, e) =>
            {
                if (e.Severity != XmlSeverityType.Error) return;
                exceptions.Add(e.Exception);
            });
            if (exceptions.Count > 0)
            {
                throw new ProjectValidationException(exceptions);
            }

            XmlElement docElem = doc.DocumentElement;

            this.OutputDirectory = docElem.Attributes["outputDir"].Value;

            if (docElem.Attributes["seed"] != null)
                this.Seed = docElem.Attributes["seed"].Value;
            else
                this.Seed = null;

            if (docElem.Attributes["debug"] != null)
                this.Debug = bool.Parse(docElem.Attributes["debug"].Value);
            else
                this.Debug = false;

            foreach (XmlElement i in docElem.ChildNodes.OfType<XmlElement>())
            {
                if (i.Name == "packer")
                {
                    Packer = new SettingItem<Packer>();
                    Packer.Load(i);
                }
                else
                {
                    ProjectModule asm = new ProjectModule();
                    asm.Load(i);
                    this.Add(asm);
                }
            }
        }
    }
}
