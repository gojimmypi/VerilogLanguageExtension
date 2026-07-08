
echo This will close all running Visual Studio instances before reinstalling the VLE VSIX.
choice /C YN /N /M "Continue and close all Visual Studio instances? [Y/N] "
if errorlevel 2 (
    echo Aborted. No Visual Studio instances were closed.
    exit /b 1
)

@echo Begin script: %TIME%
cd /d C:\workspace\VerilogLanguageExtension

@echo Stop tasks: %TIME%
taskkill /f /im devenv.exe 2>nul

@echo Restore, rebuild: %TIME%
msbuild VerilogLanguageExtension.sln /restore /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
if errorlevel 1 exit /b 1

@echo Confirm templateManifest Verilog.vstemplate: %TIME%
tar -tf bin\Debug\VerilogLanguage.vsix | findstr /i "templateManifest Verilog.vstemplate"
if errorlevel 1 exit /b 1

@echo Uninstall: %TIME%
"%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" /quiet /rootSuffix:Exp /uninstall:CF0DCF14-5B8F-4B42-8386-9D37BB99F98E

@echo Hard delete: %TIME%
rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\ComponentModelCache" 2>nul
rmdir /s /q "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\TemplateEngineHost" 2>nul

@echo Install: %TIME%
"%VSINSTALLDIR%Common7\IDE\VSIXInstaller.exe" /quiet /rootSuffix:Exp bin\Debug\VerilogLanguage.vsix
if errorlevel 1 exit /b 1

@echo Update config: %TIME%
"%VSINSTALLDIR%Common7\IDE\devenv.exe" /rootsuffix Exp /updateconfiguration

@echo Install templates: %TIME%
"%VSINSTALLDIR%Common7\IDE\devenv.exe" /rootsuffix Exp /installvstemplates

@echo There should be only ONE installed instance:
where /r "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\Extensions" templateManifest*.vstman

@echo There should be only ONE installed instance:
where /r "%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_0767a518Exp\Extensions" Verilog.vstemplate

@echo Lanuch Visual Studio: %TIME%
"%VSINSTALLDIR%Common7\IDE\devenv.exe" /rootsuffix Exp /log "%TEMP%\vle-activity.xml"

