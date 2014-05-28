---
layout: page
---
{% include setup %}

ConfuserEx is an open-source protector for .NET applications.
It is the successor of [Confuser](http://confuser.codeplex.com) project.

---
<div class="row">
  <div class="col-md-12">
    <img class="img-responsive" alt="Screenshot of Command-line interface" src="{{ site.url }}/assets/screenshot1.png">
    <small>Command-line interface</small>
  </div>
  <!--  Umm... not really. WIP.
  <div class="col-md-6">
    <img class="img-responsive" alt="Screenshot of Graphical interface" src="{{ site.url }}/assets/screenshot2.png">
    <small>Graphical interface</small>
  </div>
  -->
</div>
---

Features
--------
ConfuserEx supports .NET Framework from 2.0 - 4.5 and Mono (and other .NET platforms if enough request!).
It supports most of the protections you'll find in commerical protectors, and some more!

<div class="container-fluid">
  <p class="row">
    <ul class="col-md-4">
      <li>Symbol renaming</li>
      <li>WPF/BAML renaming</li>
      <li>Control flow obfuscation</li>
      <li>Method reference hiding</li>
    </ul>
    <ul class="col-md-4">
      <li>Anti debuggers/profilers</li>
      <li>Anti memory dumping</li>
      <li>Anti tampering (method encryption)</li>
      <li>Embedding dependency</li>
    </ul>
    <ul class="col-md-4">
      <li>Constant encryption</li>
      <li>Resource encryption</li>
      <li>Compressing output</li>
      <li>Extensible plugin API</li>
    </ul>
  </p>
</div>

---
<div class="row">
  <div class="col-md-6">
    <img class="img-responsive" alt="Assembly loaded in ILSpy before protection" src="{{ site.url }}/assets/prot1.png">
    <small>Before protection</small>
  </div>
  <!--
      Umm... Actually I think it's a bit unfair to use invalid metadata protection in this image,
      but I can assure you that, even if you don't use invalid metadata, the protection is still
      very good! :)
  -->
  <div class="col-md-6">
    <img class="img-responsive" alt="Assembly loaded in ILSpy after protection" src="{{ site.url }}/assets/prot2.png">
    <small>After protection</small>
  </div>
</div>
---

Downloads
---------
You could obtain the latest source code and releases at [GitHub project page](https://github.com/yck1509/ConfuserEx/releases).
ConfuserEx requires only .NET Framework 3.5 to run.
It might be helpful to read the [FAQ]({{ site.url }}/faq/)!

---

Contribution
------------
ConfuserEx is licensed under [MIT license](http://opensource.org/licenses/MIT), 
so you're free to fork and modify it to suit your need!
You could also contribute to the project by creating pull requests and [reporting bugs]({{ site.url }}/issues/)!

---

Dontation
---------
If you find ConfuserEx is useful to you, feel free to support the project by making a donation!  
`BTC : 12wzXhmMtjhn9dEFXdtoVAFwSx3MmV4Afx`  
`DOGE: DM8NpgvQhzPKcdca6AbSxPiV9QCcix9BYJ`