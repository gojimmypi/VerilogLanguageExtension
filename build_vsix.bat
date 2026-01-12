cls
rmdir /s /q obj
rmdir /s /q bin

echo "This file:"
type build_vsix.bat
echo "Interesting source files:"

type SnapshotExporter\GuidList.cs
type SnapshotExporter\VerilogLanguagePackage.cs
type SnapshotExporter\PkgCmdIDList.cs
type VerilogLanguage.vsct
type VerilogLanguage.csproj
type VSPackage.resx

msbuild -version

msbuild VerilogLanguage.csproj /restore /t:Rebuild /p:Configuration=Debug /p:Platform=AnyCPU /v:minimal

mkdir .\bin\Debug\vsix-contents
copy  .\bin\Debug\VerilogLanguage.vsix .\bin\Debug\VerilogLanguage.zip
tar -xf  .\bin\Debug\VerilogLanguage.zip -C .\bin\Debug\vsix-contents
dir /s /b .\bin\Debug\vsix-contents

echo extension.vsixmanifest contents:
type .\bin\Debug\vsix-contents\extension.vsixmanifest

echo VerilogLanguage.pkgdef contents:
type .\bin\Debug\vsix-contents\VerilogLanguage.pkgdef

dir /s /b *.ctmenu
dir /s /b *.cto
link /dump /nologo /resources bin\Debug\VerilogLanguage.dll | findstr /i CTMENU
