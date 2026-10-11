@echo off
rem Builds bin\GitGraphLite.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem Usage: build.cmd        build the app
rem        build.cmd test   build the app, then build and run the tests
setlocal
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist bin mkdir bin
"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /win32icon:src\app.ico /out:bin\GitGraphLite.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs || exit /b 1
if /i not "%1"=="test" exit /b 0
"%CSC%" /nologo /codepage:65001 /target:exe /out:bin\Tests.exe src\Git.cs src\GraphLayout.cs src\RepoWatcher.cs src\Orca.cs tests\Tests.cs || exit /b 1
bin\Tests.exe
