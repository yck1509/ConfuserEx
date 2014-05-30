@echo off
%windir%\microsoft.net\framework\v4.0.30319\msbuild ..\Confuser2.sln /p:Configuration=Release "/p:Platform=Any CPU"
@IF %ERRORLEVEL% NEQ 0 GOTO err
7z a ConfuserEx_bin.zip -tzip @files.lst
@exit /B 0
:err
@PAUSE
@exit /B 1