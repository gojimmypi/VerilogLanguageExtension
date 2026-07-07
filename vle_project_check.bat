
echo This will close all running Visual Studio instances before reinstalling the VLE VSIX.
choice /C YN /N /M "Continue and close all Visual Studio instances? [Y/N] "
if errorlevel 2 (
    echo Aborted. No Visual Studio instances were closed.
    exit /b 1
)

cd /d C:\workspace\VerilogLanguageExtension

taskkill /f /im devenv.exe 2>nul

msbuild VerilogLanguageExtension.sln /restore /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
if errorlevel 1 exit /b 1

tar -tf bin\Debug\VerilogLanguage.vsix | findstr /i "templateManifest Verilog.vstemplate"
if errorlevel 1 exit /b 1

"%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" /quiet /rootSuffix:Exp /uninstall:CF0DCF14-5B8F-4B42-8386-9D37BB99F98E

rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\ComponentModelCache" 2>nul
rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\TemplateEngineHost" 2>nul

"%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" /quiet /rootSuffix:Exp bin\Debug\VerilogLanguage.vsix
if errorlevel 1 exit /b 1

"%VSINSTALLDIR%Common7\IDE\devenv.exe" /rootsuffix Exp /updateconfiguration
"%VSINSTALLDIR%Common7\IDE\devenv.exe" /rootsuffix Exp /installvstemplates

echo "There should be only ONE installed instance:
where /r "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\Extensions" templateManifest*.vstman

echo "There should be only ONE installed instance:
where /r "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\Extensions" Verilog.vstemplate

"%VSINSTALLDIR%Common7\IDE\devenv.exe" /rootsuffix Exp /log "%TEMP%\vle-activity.xml"

