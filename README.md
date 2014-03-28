ConfuserEx
========
ConfuserEx is a open-source protector for .NET applications.
It is the successor of [Confuser](http://confuser.codeplex.com) project.

Features
--------
* Supports .NET Framework 2.0/3.0/3.5/4.0/4.5
* Symbol renaming
* Protection against debuggers/profilers
* Protection against memory dumping
* Control flow obfuscation
* Reference hiding proxies
* Extensible plugin API
* Many more are coming!

Usage
-----
`Confuser.CLI <path to project file>`

The project file is a ConfuserEx Project (*.crproj).
The format of project file can be found in ProjectFormat.md

Bug Report
----------
If your application does not work with ConfuserEx, feel free to submit a bug report!

If you decided to submit a bug report, please include the following information:

1. The version of ConfuserEx you are using.
2. The protection and packer settings you used.
3. The input and output sample files that does not work.  
   If you can't disclose the application, you can try reducing it to a minimum case that does not work in ConfuserEx,  
   or you can sent it to me through email to <confuser.net@gmail.com> if you prefer not to disclose it publicly.
4. If it sometimes works and sometimes does not work, it might be better to include the seed since ConfuserEx uses random mechanism in protection.


License
-------
See LICENSE file for details.

Credits
-------
**[0xd4d](http://bitbucket.org/0xd4d)** for his awesome work and extensive knowledge!  
Members of **[Black Storm Forum](http://board.b-at-s.info/)** for their help!