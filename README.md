Neo ConfuserEx
--------


Neo ConfuserEx is the successor of [ConfuserEx](https://yck1509.github.io/ConfuserEx/) project, an open source C# obfuscator which uses its own fork of [dnlib](https://github.com/0xd4d/dnlib/) for assembly manipulation. Neo ConfuserEx handles most of the dotnet app, supports all elligible .NET Frameworks and provide decent obfuscation on your file. If you have any questions or issues please let me know [there](https://github.com/XenocodeRCE/neo-ConfuserEx/issues). You can download latest official release [here](https://github.com/XenocodeRCE/neo-ConfuserEx/releases) and latest build [here](https://ci.appveyor.com/project/XenocodeRCE/neo-confuserex/build/artifacts).

<p align="center">
  
[![Build status](https://img.shields.io/appveyor/ci/gruntjs/grunt.svg)](https://ci.appveyor.com/project/XenocodeRCE/neo-confuserex/build/artifacts)

</p>

Documentation
--------

Please have a look at our [WIKI](https://github.com/XenocodeRCE/neo-ConfuserEx/wiki) page where each of our protection got a clear introduction and explanation about its functionnality.

Obfuscation Features
--------
* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5 and up to 4.7.2
* Symbol renaming (Support WPF/BAML)
* Protection against **debuggers/profilers**
* Protection against **memory dumping**
* Protection against **tampering (method encryption)**
* **Control flow** obfuscation
* **Constant/resources** encryption
* **Reference hiding** proxies
* **Type scrambler** obfuscation
* Disable **decompilers**
* **Embedding dependency**
* **Compressing output**
* Extensible plugin API


Usage
-----
`Confuser.CLI <path to project file>`

The project file is a ConfuserEx Project (*.crproj).
The format of project file can be found in docs\ProjectFormat.md

You can also run the GUI application, `ConfuserEx.exe`

Bug Report
----------
See the [Issues Report](https://github.com/XenocodeRCE/neo-ConfuserEx/issues) section of this repository.


License
-------
See LICENSE file for details.

Credits
-------
**[yck1509](https://github.com/yck1509)** the one and only, original coder of [ConfuserEx](https://yck1509.github.io/ConfuserEx/)

**[0xd4d](https://github.com/0xd4d)** for his awesome work and extensive knowledge!  

Members of **[Black Storm Forum](http://board.b-at-s.info/)** for their help!
