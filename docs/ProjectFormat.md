ConfuserEx Project Format
=======================

ConfuserEx projects (*.crproj) is an XML formatted document describing the ConfuserEx project.

For details of the format, please refer to the XML schema at `Confuser.Core\Project\ConfuserPrj.xsd`.

Element `project`
-----------------

Element `project` is the root element of the project document.

**Attributes:**

`outputDir`:  
The directory which ConfuserEx stores the output files.

`baseDir`:  
The base directory of all relative path used in the project document.
If this attribute value is also a relative path, the result depends on the implementation.
In the offical implementation (Confuser.CLI), it would be based on the location of project file.
 
`seed`:  
The seed of the random generator in protection process.

`debug`:  
Indicates whether the debug symbols (*.pdb) are generated.
Currently unused.

**Elements:**

`rule`:  
The global protection rules applied to all modules.
Optional.

`packer`:  
The packer setting.
Optional.
Currently unused.

`module`:  
The settings of individual modules.

`probePath`:  
The directories in which ConfuserEx searches for dependencies.


Element `module`
----------------

Element `module` describes the settings of individual modules.

**Attributes:**

`path`:  
The path of the module.

`snKey`:  
The path to the Strong Name Key used to sign the module.
Optional.

`snKeyPass`:  
The password of the SNK if a PFX container is used in `snKey`.
Optional.

**Elements:**

`rule`:  
The protection rules applied to the module.


Element `rule`
--------------

Element `rule` describes a rule that determine how the protections are applied.

**Attributes:**

`inherit`:  
Indicates whether this rule inherits the settings from the previous rules.
Default to `true`.

`pattern`:  
The pattern expression used to match the target components of this rule.

`preset`:  
The protection preset of the rule.
Possible values are `none`, `minimum`, `normal`, `aggressive` and `maximum`.
Default to `none`.

**Elements:**

`protection`:  
The protection settings.

Element `protection` and `packer`
---------------------------------

Element `protection` and `packer` describe the settings of individual protection/packer.

**Attributes:**

`action`:  
Indicates whether the protection are to be added or removed from settings.
Possible values are `add` and `remove`.
Default to `add`.

`id`:  
The identifier of the protection/packer.

**Elements:**

`argument`:  
The arguments that passed to the protection.
Optional.

Element `argument`
------------------

An argument that is passed to a protection.

**Attributes:**

`name`:
The name of the argument.

`value`:
The value of the argument.

Applying rules
--------------
The rules are applied from global rules (in `project` element) to local rules (in `module` element), from begin to end.
ConfuserEx will keep a list of protections for every items, and applies the rules in order.

For each rules, ConfuserEx will do:

1. If the item does not match with the rule's pattern, skip the rule.
2. If the rule does not inherit previous settings (i.e. no `inherit`), clear the marking on the item.
3. Mark the items with the protections contained in the specified `preset` value of the rule.
4. For each protection settings in the rule:
5. If `action` is remove, remove the protection from the marking.
6. If `action` is add, add the protection settings to marking.

The pattern is a simple function-based expression that evaluated for every items. If it is evaluated to `true`, the item is matched with the pattern.

Here are some example expressions:
`true`: Matches all items.
`name('X')`: Matches all items that has name 'X'.
`member-type('type') and full-name('NS.Type')`: Matches types that have full name 'NS.Type'.

Examples
----------------
ConfuserEx projects that work for ILSpy and PaintDotNet can be found under `additional` directory as examples.
