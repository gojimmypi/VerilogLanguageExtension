# Project Templates

The shipped Visual Studio project-template package is:

```
ProjectTemplates/CSharp/1033/VerilogProject.zip
```

The source-of-truth files live here:

```
AddedExtensionProjectTemplates/VerilogProject/
```

Visual Studio does not read the loose files under `AddedExtensionProjectTemplates` after install. The VSIX manifest packages the generated template zip:

```
<Asset Type="Microsoft.VisualStudio.ProjectTemplate"
       d:Source="File"
       Path="ProjectTemplates\CSharp\1033\VerilogProject.zip" />
```

The `CSharp/1033` folder is intentional. It keeps the VSIX project template in the normal Visual Studio language/locale layout instead of a flat `ProjectTemplates` directory.

## Regenerate the template zip

From the repository root:

```
Get-ChildItem .\tools\templates -Filter *.ps1 | Unblock-File
.\tools\templates\Build-ProjectTemplates.ps1
.\tools\templates\Test-ProjectTemplate.ps1
```

The build script reads `AddedExtensionProjectTemplates/VerilogProject/Verilog.vstemplate`, copies only the source files referenced by the template, and writes `ProjectTemplates/CSharp/1033/VerilogProject.zip`.

The validation script checks for the issue #30 class of problems:

- exactly one `.vstemplate` in the zip
- `Project File=` points to the real source project file, not `$safeprojectname$.csproj`
- `TargetFileName=` uses `$safeprojectname$.csproj`
- all listed `ProjectItem` files exist in the zip
- icon and preview files exist
- imported `.csproj` fragments exist
- stale files like `MyTemplate.vstemplate`, `.csproj.user`, `bin/`, `obj/`, `.vs/`, and logs are absent
- local paths like `C:\workspace` or `C:\Users\gojimmypi` are absent
- template entries do not contain spaces or `%20`

## Normal edit workflow

1. Edit files under `AddedExtensionProjectTemplates/VerilogProject/`.
2. Run `tools/templates/Build-ProjectTemplates.ps1`.
3. Run `tools/templates/Test-ProjectTemplate.ps1`.
4. Build the VSIX.

The generated zip is a source-controlled build input for the VSIX and should be committed when the template changes.
