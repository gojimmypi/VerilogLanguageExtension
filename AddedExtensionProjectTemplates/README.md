# Added Extension Project Templates

Each of the subdirectories here contain files that are created during `File - New Project` in Visual Studio.

These directories are NOT part of the Verilog Language Extension development solution and should be zipped up (via the Export Template...) and included in the `.\ProjectTemplates\` directory for distribution with installation files.

If these projects are added to the main solution, a large number of (probably undesired) build configurations for the FPGA board will be added to that solution.

Instead, when editing these project templates, open the respective solution file. For example the [Verilog Project.sln](./Verilog Project/Verilog Project.sln) in 
`AddedExtensionProjectTEmplate/Verilog Project/`

See also the [Verilog Project/README.md](./Verilog Project/README.md) file.

## Updating the Project Template in the VSIX Installer

In the `AddedExtensionProjectTemplate/Verilog Project/` directory, open the [AddedExtensionProjectTemplate/Verilog Project/Verilog Project.sln Solution](./Verilog Project.sln) and click on `Project - Export Template`. 
Ensure the `VerilogProjectTemplate` is the project being exported! (this is typically not the default).


## Updating the Project Template Manually

Although Visual Studio should (in theory) allow an included project to be an asset in the deployed VSIX install, this behaviour at one time was not observed.

To manually install a template, from the main menu in the [Verilog Project.sln](./Verilog Project.sln) solution file and click on `Project - Export Template`. 
Ensure the `VerilogProjectTemplate` is the project being exported! (this is typically not the default).

![export_template.png](./images/export_template.png)

The only option is a read-only save to `C:\Users\%USERNAME%\Documents\Visual Studio 2019\My Exported Templates\VerilogProjectTemplate.zip`. 
Copy this file to the solution `ProjectTemplates` directory. See the [source.extension.vsixmanifest](../../source.extension.vsixmanifest) file.

For example, put the zip `C:\Users\gojimmypi\Documents\Visual Studio 2019\Templates\ProjectTemplates`

If after deleting the templates from the above directories and the template feature _still works_ for Verilog projects, 
searching:  `dir Verilog.vstemplate /s` on the C:\ drive resulted in two different versions beding apparently copied automatically by Visual Studio and/or extension installer in:

`C:\Users\gojimmypi\AppData\Local\Microsoft\VisualStudio\16.0_d46d6b9a\Extensions\1t1adxin.whf\ProjectTemplates\Verilog Project`

`C:\Users\gojimmypi\AppData\Local\Microsoft\VisualStudio\16.0_d46d6b9aExp\Extensions\gojimmypi\VerilogLanguage\0.3.4.36\ProjectTemplates\Verilog Project`

Note the second one is from the Experimental Instance of Visual Studio used during development. No clue as to what `1t1adxin.whf` is.



## Other Resources:

* [Output errors, warnings and messages from batch file in Visual Studio build event](https://stackoverflow.com/questions/29799149/output-errors-warnings-and-messages-from-batch-file-in-visual-studio-build-even)
* [Template Parameters](https://docs.microsoft.com/en-us/visualstudio/ide/template-parameters?view=vs-2019)
* [Get started with the Windows Subsystem for Linux](https://docs.microsoft.com/en-us/learn/modules/get-started-with-windows-subsystem-for-linux/)

* [How to: View, save, and configure build log files](https://docs.microsoft.com/en-us/visualstudio/ide/how-to-view-save-and-configure-build-log-files?view=vs-2019)
* [Walkthrough: Create an inline task](https://docs.microsoft.com/en-us/visualstudio/msbuild/walkthrough-creating-an-inline-task?view=vs-2019) for MSBuild
* [ProcessStartInfo.RedirectStandardOutput Property](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.redirectstandardoutput?view=netframework-4.8#System_Diagnostics_ProcessStartInfo_RedirectStandardOutput)
* [Target element (MSBuild)](https://docs.microsoft.com/en-us/visualstudio/msbuild/target-element-msbuild?view=vs-2019)
* [Task element of Target (MSBuild)](https://docs.microsoft.com/en-us/visualstudio/msbuild/task-element-msbuild?view=vs-2019)
* [Task base class](https://docs.microsoft.com/en-us/visualstudio/msbuild/task-base-class?view=vs-2019)
* [UsingTask element (MSBuild)](https://docs.microsoft.com/en-us/visualstudio/msbuild/usingtask-element-msbuild?view=vs-2019)
* [Exec task](https://docs.microsoft.com/en-gb/visualstudio/msbuild/exec-task?view=vs-2019) for MSBuid

* [Output element (MSBuild)](https://docs.microsoft.com/en-us/visualstudio/msbuild/output-element-msbuild?view=vs-2019)
* [StackOverflow: MSBuild exec task without blocking: ExecAsync](https://stackoverflow.com/questions/2387456/msbuild-exec-task-without-blocking)

* [StackOverflow: MSBuild AfterBuild messages not showing real-time](https://stackoverflow.com/questions/38125377/msbuild-afterbuild-messages-not-showing-real-time)
* [ToolTask.YieldDuringToolExecution Property](https://docs.microsoft.com/en-us/dotnet/api/microsoft.build.utilities.tooltask.yieldduringtoolexecution?view=netframework-4.8)

* [Get started with the Windows Subsystem for Linux](https://docs.microsoft.com/en-us/learn/modules/get-started-with-windows-subsystem-for-linux/)
