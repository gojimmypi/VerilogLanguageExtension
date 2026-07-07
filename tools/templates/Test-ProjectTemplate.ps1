# The VSIX ProjectTemplate asset must point at ProjectTemplates, not the generated
# VerilogProject.zip path, because the built VSIX contains the expanded template
# tree plus templateManifest0.1033.vstman.

[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$TemplateName = "VerilogProject",
    [string]$TemplateLanguage = "CSharp",
    [string]$TemplateLocale = "1033"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (!$Condition) {
        throw $Message
    }
}

function Normalize-ZipPath {
    param([string]$Path)
    return $Path.Trim().Replace("\", "/")
}

function Join-TemplatePath {
    param(
        [string]$ParentPath,
        [string]$ChildPath
    )

    if ([string]::IsNullOrWhiteSpace($ParentPath)) {
        return $ChildPath
    }

    return ($ParentPath.TrimEnd("\", "/") + "/" + $ChildPath)
}

function Assert-SimpleTargetFileName {
    param(
        [string]$TargetFileName,
        [string]$Context
    )

    if ([string]::IsNullOrWhiteSpace($TargetFileName)) {
        return
    }

    Assert-True ($TargetFileName -notlike "*\*") "$Context TargetFileName must be a file name only. Put folder paths in Folder elements: $TargetFileName"
    Assert-True ($TargetFileName -notlike "*/*") "$Context TargetFileName must be a file name only. Put folder paths in Folder elements: $TargetFileName"
}

function Read-ZipEntryText {
    param(
        [System.IO.Compression.ZipArchive]$Archive,
        [string]$EntryName
    )

    $entry = $Archive.GetEntry($EntryName)
    Assert-True ($entry -ne $null) "Zip entry not found: $EntryName"
    $stream = $entry.Open()
    try {
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-TemplateFolderItemSources {
    param(
        [System.Xml.XmlElement]$Folder,
        [System.Xml.XmlNamespaceManager]$NamespaceManager,
        [string]$ParentPath
    )

    $folderName = $Folder.GetAttribute("Name")
    Assert-True (![string]::IsNullOrWhiteSpace($folderName)) "Template Folder is missing a Name attribute."
    Assert-True ($folderName -notlike "*`$safeprojectname`$*") "Folder Name must not contain template parameters: $folderName"
    Assert-True ($folderName -notlike "* *") "Folder Name must not contain spaces: $folderName"
    Assert-True ($folderName -notlike "*\*") "Folder Name must be a single folder name: $folderName"
    Assert-True ($folderName -notlike "*/*") "Folder Name must be a single folder name: $folderName"

    $targetFolderName = $Folder.GetAttribute("TargetFolderName")
    if (![string]::IsNullOrWhiteSpace($targetFolderName)) {
        Assert-True ($targetFolderName -notlike "*\*") "Folder TargetFolderName must be a single folder name: $targetFolderName"
        Assert-True ($targetFolderName -notlike "*/*") "Folder TargetFolderName must be a single folder name: $targetFolderName"
    }

    $folderPath = Join-TemplatePath $ParentPath $folderName
    $sources = @()

    foreach ($item in $Folder.SelectNodes("vst:ProjectItem", $NamespaceManager)) {
        Assert-SimpleTargetFileName $item.TargetFileName "ProjectItem under $folderPath"
        $sources += (Join-TemplatePath $folderPath $item.InnerText)
    }

    foreach ($childFolder in $Folder.SelectNodes("vst:Folder", $NamespaceManager)) {
        $sources += @(Get-TemplateFolderItemSources $childFolder $NamespaceManager $folderPath)
    }

    return $sources
}

# Keep validation aligned with the shipped VSIX project-template path.
$templateZip = Join-Path $RepoRoot ("ProjectTemplates\" + $TemplateLanguage + "\" + $TemplateLocale + "\" + $TemplateName + ".zip")
Assert-True (Test-Path $templateZip) "Template zip not found: $templateZip"

$archive = [System.IO.Compression.ZipFile]::OpenRead($templateZip)
try {
    $entryNames = @($archive.Entries | Where-Object { $_.FullName -notlike "*/" } | ForEach-Object { $_.FullName })
    $entrySet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entryName in $entryNames) {
        Assert-True ($entryName -notlike "* *") "Template zip entry contains a space and may be percent-encoded by VSIX packaging: $entryName"
        Assert-True ($entryName -notlike "*%20*") "Template zip entry contains percent-encoded spaces: $entryName"
        [void]$entrySet.Add((Normalize-ZipPath $entryName))
    }

    $templates = @($entryNames | Where-Object { $_ -like "*.vstemplate" })
    Assert-True ($templates.Count -eq 1) "Expected exactly one .vstemplate file; found $($templates.Count): $($templates -join ', ')"

    $templateText = Read-ZipEntryText $archive $templates[0]
    [xml]$templateXml = $templateText
    $ns = New-Object System.Xml.XmlNamespaceManager($templateXml.NameTable)
    $ns.AddNamespace("vst", "http://schemas.microsoft.com/developer/vstemplate/2005")

    $project = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateContent/vst:Project", $ns)
    Assert-True ($project -ne $null) "TemplateContent/Project was not found."
    Assert-True (![string]::IsNullOrWhiteSpace($project.File)) "Project File attribute is empty."
    Assert-True ($project.File -notlike "*`$safeprojectname`$*") "Project File attribute must name the source file, not a template parameter."
    Assert-True ($project.TargetFileName -like "*`$safeprojectname`$*") "Project TargetFileName should use `$safeprojectname$."

    $projectFile = Normalize-ZipPath $project.File
    Assert-True ($entrySet.Contains($projectFile)) "Project source file missing from zip: $projectFile"

    $icon = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:Icon", $ns)
    Assert-True ($icon -ne $null) "TemplateData/Icon was not found."
    Assert-True ($entrySet.Contains((Normalize-ZipPath $icon.InnerText))) "Icon file missing from zip: $($icon.InnerText)"

    $preview = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:PreviewImage", $ns)
    if ($preview -ne $null) {
        Assert-True ($entrySet.Contains((Normalize-ZipPath $preview.InnerText))) "PreviewImage file missing from zip: $($preview.InnerText)"
    }

    $requiredTemplateDataNodes = @(
        "TemplateID",
        "TemplateGroupID"
    )
    foreach ($nodeName in $requiredTemplateDataNodes) {
        $node = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:$nodeName", $ns)
        Assert-True ($node -ne $null) "TemplateData/$nodeName was not found."
        Assert-True (![string]::IsNullOrWhiteSpace($node.InnerText)) "TemplateData/$nodeName is empty."
    }

    foreach ($item in $project.SelectNodes("vst:ProjectItem", $ns)) {
        Assert-SimpleTargetFileName $item.TargetFileName "Root ProjectItem"
        $source = Normalize-ZipPath $item.InnerText
        Assert-True ($source -notlike "*`$safeprojectname`$*") "ProjectItem source must not contain template parameters: $source"
        Assert-True ($source -notlike "*/*") "Root ProjectItem source must be a file name only. Put folder paths in Folder elements: $source"
        Assert-True ($entrySet.Contains($source)) "ProjectItem source file missing from zip: $source"
    }

    foreach ($folder in $project.SelectNodes("vst:Folder", $ns)) {
        foreach ($sourcePath in (Get-TemplateFolderItemSources $folder $ns "")) {
            $source = Normalize-ZipPath $sourcePath
            Assert-True ($source -notlike "*`$safeprojectname`$*") "ProjectItem source must not contain template parameters: $source"
            Assert-True ($entrySet.Contains($source)) "ProjectItem source file missing from zip: $source"
        }
    }

    $bannedPatterns = @(
        "*/.vs/*",
        "*/bin/*",
        "*/obj/*",
        "*.csproj.user",
        "relentless.log",
        "VerilogProjectTemplate.csproj.new",
        "_not_used_Makefile-ULX3S-12F.mk",
        "MyTemplate.vstemplate",
        "Verilog Project.csproj"
    )
    foreach ($entryName in $entryNames) {
        foreach ($pattern in $bannedPatterns) {
            Assert-True ($entryName -notlike $pattern) "Banned generated/stale file is present in template zip: $entryName"
        }
    }

    $projectText = Read-ZipEntryText $archive $projectFile
    [xml]$projectXml = $projectText
    foreach ($import in $projectXml.Project.Import) {
        if ([string]::IsNullOrWhiteSpace($import.Project)) {
            continue
        }
        $importPath = Normalize-ZipPath ($import.Project.Replace(".\", ""))
        Assert-True ($entrySet.Contains($importPath)) "Imported project file missing from zip: $importPath"
    }

    $badLocalPatterns = @("C:\workspace", "C:\Users\gojimmypi", "/mnt/c/workspace")
    foreach ($entryName in $entryNames) {
        if ($entryName -match "\.(png|ico)$") {
            continue
        }
        $text = Read-ZipEntryText $archive $entryName
        foreach ($pattern in $badLocalPatterns) {
            Assert-True ($text -notlike "*$pattern*") "Local machine path found in $entryName: $pattern"
        }
    }

    Write-Host "Template validation passed: $templateZip"
}
finally {
    $archive.Dispose()
}
