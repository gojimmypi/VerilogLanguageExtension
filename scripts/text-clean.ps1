$ErrorActionPreference = "Stop"

$files = @(
    '.github\workflows\build-verilog-language-extension.yml',
    'AddedExtensionProjectTemplates\LiteX\LiteX.csproj',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\AssemblyInfo.cs',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\build\vs-build.bat',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\build\vs-clean.bat',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\build\vs-prog.bat',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\ProjectTemplate-LiteX.csproj',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\ProjectTemplate-LiteX.vstemplate',
    'AddedExtensionProjectTemplates\ProjectTemplate-LiteX\Properties\AssemblyInfo.cs',
    'AddedExtensionProjectTemplates\Verilog Project\App.config',
    'AddedExtensionProjectTemplates\Verilog Project\boards\icebreaker\README.md',
    'AddedExtensionProjectTemplates\Verilog Project\boards\icestick\README.md',
    'AddedExtensionProjectTemplates\Verilog Project\boards\orangecrab\blink.v',
    'AddedExtensionProjectTemplates\Verilog Project\boards\orangecrab\orangecrab_r0.1.pcf',
    'AddedExtensionProjectTemplates\Verilog Project\boards\orangecrab\orangecrab_r0.2.pcf',
    'AddedExtensionProjectTemplates\Verilog Project\build\pstool.ps1',
    'AddedExtensionProjectTemplates\Verilog Project\build\vs-clean.bat',
    'AddedExtensionProjectTemplates\Verilog Project\build\vs-prog.bat',
    'AddedExtensionProjectTemplates\Verilog Project\Program.cs',
    'AddedExtensionProjectTemplates\Verilog Project\ProjectPlatform\Config.csproj',
    'AddedExtensionProjectTemplates\Verilog Project\ProjectPlatform\Project_iCEBreaker.csproj',
    'AddedExtensionProjectTemplates\Verilog Project\ProjectPlatform\Project_ULX3S.csproj',
    'AddedExtensionProjectTemplates\Verilog Project\ProjectPlatform\UserDefined.csproj',
#   'AddedExtensionProjectTemplates\Verilog Project\ProjectTemplate.csproj',
    'AddedExtensionProjectTemplates\Verilog Project\Properties\AssemblyInfo.cs',
    'AddedExtensionProjectTemplates\Verilog Project\README.md',
    'AddedExtensionProjectTemplates\Verilog Project\top_icebreaker.v',
    'AddedExtensionProjectTemplates\Verilog Project\Verilog Project.csproj',
    'AddedExtensionProjectTemplates\Verilog Project\Verilog Project.sln',
    'AddedExtensionProjectTemplates\Verilog Project\Verilog.vstemplate',
    'CODE_OF_CONDUCT.md',
    'CppProperties.json',
    'Example-Projects\Verilog45\Verilog45.sln',
    'Example-Projects\Verilog45\Verilog45\Program.cs',
    'Example-Projects\Verilog45\Verilog45\top_icebreaker.v',
    'Example-Projects\Verilog45\Verilog45\Verilog45.csproj',
    'Lattice_securelyfitz.md',
    'LICENSE.md',
    'refresh-ci-check.ps1',
    'RELEASE_NOTES.md',
    'TestFiles\anothertest - Copy.v',
    'TestFiles\bracketest.v',
    'TestFiles\src\README.md',
    'TestFiles\test - bigfile.v',
    'TestFiles\test.v',
    'VerilogLanguage.csproj',
    'VSPackage.resx'
)

function Test-SameBytes {
    param(
        [byte[]]$A,
        [byte[]]$B
    )

    if ($A.Length -ne $B.Length) {
        return $false
    }

    for ($i = 0; $i -lt $A.Length; $i++) {
        if ($A[$i] -ne $B[$i]) {
            return $false
        }
    }

    return $true
}

foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) {
        Write-Warning "Missing file: $file"
        continue
    }

    [byte[]]$original = [System.IO.File]::ReadAllBytes($file)
    [byte[]]$bytes = $original

    # Refuse to touch UTF-16 files with BOM. The listed failures are UTF-8/text,
    # and byte-level CR/LF conversion would not be safe for UTF-16.
    if ($bytes.Length -ge 2) {
        if (($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or
            ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF)) {
            throw "Refusing to byte-normalize UTF-16 file: $file"
        }
    }

    # Strip UTF-8 BOM.
    if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and
        $bytes[1] -eq 0xBB -and
        $bytes[2] -eq 0xBF) {

        if ($bytes.Length -eq 3) {
            $bytes = @()
        } else {
            $bytes = $bytes[3..($bytes.Length - 1)]
        }
    }

    # Normalize CRLF / LF / CR to CRLF, preserving all other bytes.
    $out = New-Object 'System.Collections.Generic.List[byte]'

    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $b = $bytes[$i]

        if ($b -eq 0x0D) {
            if (($i + 1) -lt $bytes.Length -and $bytes[$i + 1] -eq 0x0A) {
                $i++
            }

            $out.Add(0x0D)
            $out.Add(0x0A)
        } elseif ($b -eq 0x0A) {
            $out.Add(0x0D)
            $out.Add(0x0A)
        } else {
            $out.Add($b)
        }
    }

    # Ensure final newline.
    if ($out.Count -eq 0 -or
        -not ($out.Count -ge 2 -and
              $out[($out.Count - 2)] -eq 0x0D -and
              $out[($out.Count - 1)] -eq 0x0A)) {

        $out.Add(0x0D)
        $out.Add(0x0A)
    }

    [byte[]]$updated = $out.ToArray()

    if (-not (Test-SameBytes $original $updated)) {
        [System.IO.File]::WriteAllBytes($file, $updated)
        Write-Host "fixed: $file"
    }
}
