[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$TemplateName = "VerilogProject",
    [string]$TemplateLanguage = "CSharp",
    [string]$TemplateLocale = "1033"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Join-TemplatePath {
    param(
        [string]$ParentPath,
        [string]$ChildPath
    )

    if ([string]::IsNullOrWhiteSpace($ParentPath)) {
        return $ChildPath
    }

    return ($ParentPath.TrimEnd("\", "/") + "\" + $ChildPath)
}

function Add-TemplateFile {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return
    }

    $normalized = $RelativePath.Trim().Replace("/", "\")
    if ($normalized.Contains("..")) {
        throw "Template path escapes the template root: $RelativePath"
    }
    # Keep template source paths space-free because VSIX packaging can percent-encode spaces.
    if ($normalized.Contains(" ")) {
        throw "Template source path must not contain spaces: $RelativePath"
    }
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        throw "Template path must be relative: $RelativePath"
    }
    if ($normalized -like "*`$safeprojectname`$*") {
        throw "Template source path must not contain template parameters: $RelativePath"
    }

    [void]$Set.Add($normalized)
}

function Add-TemplateFolderFiles {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [System.Xml.XmlElement]$Folder,
        [System.Xml.XmlNamespaceManager]$NamespaceManager,
        [string]$ParentPath
    )

    $folderName = $Folder.GetAttribute("Name")
    if ([string]::IsNullOrWhiteSpace($folderName)) {
        throw "Template Folder is missing a Name attribute."
    }

    $folderPath = Join-TemplatePath $ParentPath $folderName

    foreach ($item in $Folder.SelectNodes("vst:ProjectItem", $NamespaceManager)) {
        Add-TemplateFile $Set (Join-TemplatePath $folderPath $item.InnerText)
    }

    foreach ($childFolder in $Folder.SelectNodes("vst:Folder", $NamespaceManager)) {
        Add-TemplateFolderFiles $Set $childFolder $NamespaceManager $folderPath
    }
}

$sourceDir = Join-Path $RepoRoot ("AddedExtensionProjectTemplates\" + $TemplateName)
$templateFile = Join-Path $sourceDir "Verilog.vstemplate"
# Use the standard Visual Studio project-template language/locale layout.
$outputDir = Join-Path $RepoRoot ("ProjectTemplates\" + $TemplateLanguage + "\" + $TemplateLocale)
$outputZip = Join-Path $outputDir ($TemplateName + ".zip")
$stageDir = Join-Path ([System.IO.Path]::GetTempPath()) ("vle-project-template-" + [System.Guid]::NewGuid().ToString("N"))

if (!(Test-Path $templateFile)) {
    throw "Template file not found: $templateFile"
}

[xml]$templateXml = Get-Content -LiteralPath $templateFile -Raw -Encoding UTF8
$ns = New-Object System.Xml.XmlNamespaceManager($templateXml.NameTable)
$ns.AddNamespace("vst", "http://schemas.microsoft.com/developer/vstemplate/2005")

$files = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
Add-TemplateFile $files "Verilog.vstemplate"

$icon = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:Icon", $ns)
if ($icon -ne $null) {
    Add-TemplateFile $files $icon.InnerText
}

$preview = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateData/vst:PreviewImage", $ns)
if ($preview -ne $null) {
    Add-TemplateFile $files $preview.InnerText
}

$project = $templateXml.SelectSingleNode("/vst:VSTemplate/vst:TemplateContent/vst:Project", $ns)
if ($project -eq $null) {
    throw "TemplateContent/Project was not found."
}
Add-TemplateFile $files $project.File

foreach ($item in $project.SelectNodes("vst:ProjectItem", $ns)) {
    Add-TemplateFile $files $item.InnerText
}

foreach ($folder in $project.SelectNodes("vst:Folder", $ns)) {
    Add-TemplateFolderFiles $files $folder $ns ""
}

try {
    if (Test-Path $stageDir) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stageDir | Out-Null

    foreach ($relative in ($files | Sort-Object)) {
        $sourcePath = Join-Path $sourceDir $relative
        if (!(Test-Path $sourcePath -PathType Leaf)) {
            throw "Template source file is missing: $relative"
        }

        $destPath = Join-Path $stageDir $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destPath) -Force | Out-Null
        Copy-Item -LiteralPath $sourcePath -Destination $destPath -Force
    }

    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    if (Test-Path $outputZip) {
        Remove-Item -LiteralPath $outputZip -Force
    }

    Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $outputZip -CompressionLevel Optimal -Force
    Write-Host "Wrote $outputZip"
}
finally {
    if (Test-Path $stageDir) {
        Remove-Item -LiteralPath $stageDir -Recurse -Force
    }
}
