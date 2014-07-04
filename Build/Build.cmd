@echo off

IF NOT EXIST ..\ConfuserEx.snk COPY ..\ConfuserEx_default.snk ..\ConfuserEx.snk

%windir%\microsoft.net\framework\v4.0.30319\msbuild ..\Confuser2.sln /p:Configuration=Release "/p:Platform=Any CPU" /p:DefineConstants="%1"

IF %ERRORLEVEL% NEQ 0 GOTO err

7z a ConfuserEx_bin.zip -tzip @files.lst
EXIT /B 0

:err
PAUSE
EXIT /B 1