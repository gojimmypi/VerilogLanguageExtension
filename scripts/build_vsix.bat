@echo off
setlocal

pushd "%~dp0.."
if errorlevel 1 exit /b 1

cls
if exist obj rmdir /s /q obj
if errorlevel 1 exit /b 1
if exist bin rmdir /s /q bin
if errorlevel 1 exit /b 1

echo "This file:"
type scripts\build_vsix.bat
echo "Interesting source files:"

type SnapshotExporter\GuidList.cs
if errorlevel 1 exit /b 1
type VerilogLanguagePackage.cs
if errorlevel 1 exit /b 1
type SnapshotExporter\PkgCmdIDList.cs
if errorlevel 1 exit /b 1
type VerilogLanguagePackage.vsct
if errorlevel 1 exit /b 1
type VerilogLanguage.csproj
if errorlevel 1 exit /b 1
type VSPackage.resx
if errorlevel 1 exit /b 1

msbuild -version
if errorlevel 1 exit /b 1

msbuild VerilogLanguage.csproj /restore /t:Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /v:minimal
if errorlevel 1 exit /b 1

mkdir .\bin\Debug\vsix-contents
if errorlevel 1 exit /b 1
copy  .\bin\Debug\VerilogLanguage.vsix .\bin\Debug\VerilogLanguage.zip
if errorlevel 1 exit /b 1
tar -xf  .\bin\Debug\VerilogLanguage.zip -C .\bin\Debug\vsix-contents
if errorlevel 1 exit /b 1
dir /s /b .\bin\Debug\vsix-contents
if errorlevel 1 exit /b 1

echo extension.vsixmanifest contents:
type .\bin\Debug\vsix-contents\extension.vsixmanifest
if errorlevel 1 exit /b 1

echo VerilogLanguage.pkgdef contents:
type .\bin\Debug\vsix-contents\VerilogLanguage.pkgdef
if errorlevel 1 exit /b 1

dir /s /b *.ctmenu
dir /s /b *.cto
link /dump /nologo /resources bin\Debug\VerilogLanguage.dll | findstr /i CTMENU
if errorlevel 1 exit /b 1
