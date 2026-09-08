VSIX Repair Tool
================

``scripts/Repair-VerilogLanguageVSIX.ps1`` repairs a stale per-user Visual
Studio VSIX registration and can reinstall the supplied VSIX into selected
Visual Studio instances.

The tool exists for cases where normal Extension Manager behavior is
inconsistent. A representative failure is:

* VLE is working in Visual Studio 2022.
* VLE does not appear in Visual Studio 2026 under **Installed -> All**.
* Installing the VSIX for Visual Studio 2026 reports:
  ``This extension is already installed to all applicable products.``

For ordinary installation, continue to prefer:

**Visual Studio -> Extensions -> Manage Extensions -> Browse**

The repair script is for recovery, not the normal installation path.

Why instance IDs must be discovered
-----------------------------------

Visual Studio assigns each installation an ``instanceId``. An ID such as
``83ca6b20`` identifies one specific installation on one specific computer. It
is **not universal** and must not be copied into a distributable repair script.

The repair utility uses Microsoft's ``vswhere.exe`` to discover the installed
Visual Studio instances, reads each real ``instanceId``, and then passes it to
``VSIXInstaller.exe`` as:

.. code-block:: text

   /instanceIds:<discovered-instance-id>

The default version range is:

.. code-block:: text

   [18.0,19.0)

That targets Visual Studio 2026 (18.x) and intentionally leaves Visual Studio
2022 (17.x) alone unless the caller explicitly changes ``-VersionRange``.

Safety model
------------

The script is intentionally conservative:

* It requires a real VSIX path and validates that the file is non-empty.
* It opens the VSIX as a ZIP package and reads ``extension.vsixmanifest``.
* It reads the extension ID and version from the package; the VLE GUID is not
  hard-coded into the repair logic.
* XML DTD processing is prohibited and the XML resolver is disabled.
* It refuses to run while ``devenv.exe`` or another ``VSIXInstaller.exe`` is
  already running.
* It does not use ``/admin``.
* It does not directly delete extension directories, profile data, registry
  hives, or files under Program Files.
* It supports PowerShell ``-WhatIf`` / ``-Confirm`` through
  ``SupportsShouldProcess``.
* ``-InstallOnly`` and ``-UninstallOnly`` are mutually exclusive.
* It writes unique VSIXInstaller logs to ``%TEMP%``.
* It bounds the wait for VSIXInstaller with a configurable timeout.

Getting started
---------------

Download or build the VSIX, then close all Visual Studio instances.

From the repository root, first preview the operation:

.. code-block:: powershell

   .\scripts\Repair-VerilogLanguageVSIX.ps1 `
       -VsixPath "$env:USERPROFILE\Downloads\VerilogLanguage_0.5.1.67.vsix" `
       -WhatIf

A normal repair is:

.. code-block:: powershell

   .\scripts\Repair-VerilogLanguageVSIX.ps1 `
       -VsixPath "$env:USERPROFILE\Downloads\VerilogLanguage_0.5.1.67.vsix"

For the stale-registration failure described above, the preferred recovery is
often to remove the stale Visual Studio 2026 registration and then install from
inside Visual Studio:

.. code-block:: powershell

   .\scripts\Repair-VerilogLanguageVSIX.ps1 `
       -VsixPath "$env:USERPROFILE\Downloads\VerilogLanguage_0.5.1.67.vsix" `
       -UninstallOnly

Then start Visual Studio 2026 and use:

**Extensions -> Manage Extensions -> Browse**

Program operation
-----------------

For each selected Visual Studio instance, the default repair mode performs the
following sequence:

1. Resolve and validate the VSIX file.
2. Read the VSIX identity, version, display name, and declared installation
   targets from ``extension.vsixmanifest``.
3. Locate ``vswhere.exe``.
4. Discover Visual Studio instances inside ``-VersionRange``.
5. Optionally filter the results by ``-InstanceId``.
6. Locate ``devenv.exe`` and ``VSIXInstaller.exe`` for each usable instance.
7. Uninstall the current-user registration for the extension ID.
8. Install the supplied VSIX into the same instance.
9. Inspect VSIXInstaller logs for known fatal and success indicators.
10. Run ``devenv.exe /UpdateConfiguration`` unless it was disabled.
11. Print a summary and verification instructions.

Visual Studio discovery
-----------------------

``Get-VsWherePath`` searches common Visual Studio Installer locations under
``ProgramFiles(x86)`` and ``ProgramFiles`` and then falls back to
``Get-Command vswhere.exe``.

``Get-VisualStudioInstances`` asks ``vswhere`` for products in the requested
version range. Records without an instance ID, installation path, or
``devenv.exe`` are skipped.

The script prefers the ``VSIXInstaller.exe`` shipped with the specific Visual
Studio instance. If it cannot find that executable, it can fall back to the
shared installer discovered from ``vswhere -property enginePath``.

Parameters
----------

``-VsixPath <path>``
    Required. Path to the VSIX package. The identity and version are read from
    this package.

``-VersionRange <range>``
    Visual Studio version range passed to ``vswhere``. Default:
    ``[18.0,19.0)``.

``-InstanceId <id>[,<id>...]``
    Optional explicit selection from the instances found inside
    ``-VersionRange``. IDs are machine-specific and should be discovered, not
    documented as constants.

``-InstallOnly``
    Skip the uninstall step and install the VSIX only.

``-UninstallOnly``
    Remove the extension registration without reinstalling. This is useful
    before installing from Visual Studio's own Extension Manager.

``-SkipUpdateConfiguration``
    Do not run ``devenv.exe /UpdateConfiguration`` after installation.

``-Interactive``
    Show VSIXInstaller UI instead of adding ``/quiet``.

``-InstallerTimeoutSeconds <seconds>``
    Maximum wait for VSIXInstaller. Valid range: 10 through 1800 seconds.
    Default: 300 seconds.

``-WhatIf``
    Show the planned uninstall/install/update-configuration actions without
    performing them.

``-Confirm``
    Request PowerShell confirmation for operations protected by
    ``ShouldProcess``.

Version-range examples
----------------------

Visual Studio 2026 only, which is the default:

.. code-block:: powershell

   -VersionRange "[18.0,19.0)"

Visual Studio 2022 only:

.. code-block:: powershell

   -VersionRange "[17.0,18.0)"

Visual Studio 2022 and Visual Studio 2026:

.. code-block:: powershell

   -VersionRange "[17.0,19.0)"

Use the broader range only when modifying both major versions is intentional.

Target one discovered instance
------------------------------

You can use ``vswhere`` manually to inspect installed instances:

.. code-block:: powershell

   $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"

   $instances = (& $vswhere -all -prerelease -products * -format json) |
       ConvertFrom-Json

   $instances |
       Select-Object instanceId, displayName, installationVersion, installationPath

Then pass a discovered ID only when you intentionally want to constrain the
repair:

.. code-block:: powershell

   .\scripts\Repair-VerilogLanguageVSIX.ps1 `
       -VsixPath ".\VerilogLanguage_0.5.1.67.vsix" `
       -InstanceId <discovered-instance-id>

Do not publish an example ID as though it were portable to another computer.

Logging and result validation
-----------------------------

Each install/uninstall action uses a unique log name containing the instance
ID, action, timestamp, and PowerShell process ID. Logs are expected under
``%TEMP%``.

If the explicitly requested log file cannot immediately be found, the script
looks for a recent VSIXInstaller log created after the action began.

The script scans log text for known fatal markers such as installation errors,
``NoApplicableSKUsException``, access-denied conditions, and failed install or
uninstall messages. It also checks for known success phrases.

An installation without a log cannot be verified and is treated as an error.
If no explicit success marker is present but no fatal error is found, the
script warns the user and instructs them to verify the extension in Visual
Studio.

Interactive mode
----------------

Use ``-Interactive`` when the VSIX Installer UI itself is useful for diagnosis:

.. code-block:: powershell

   .\scripts\Repair-VerilogLanguageVSIX.ps1 `
       -VsixPath ".\VerilogLanguage_0.5.1.67.vsix" `
       -Interactive

The script still discovers the instance IDs and supplies the correct target;
it simply omits the VSIXInstaller ``/quiet`` switch.

Manual brute-force fallback
---------------------------

The following is intentionally **text-only manual recovery guidance**. The
repair script does not automate these deletions. Use this only when
VSIXInstaller cannot remove a stale per-user registration.

1. Close every Visual Studio window. Confirm that neither ``devenv.exe`` nor
   ``VSIXInstaller.exe`` is running.
2. Run the repair script with ``-WhatIf``, or run ``vswhere.exe`` manually, to
   identify the intended Visual Studio instance. Also note the extension ID
   that the script reads from the VSIX package.
3. In File Explorer, open:

   .. code-block:: text

      %LOCALAPPDATA%\Microsoft\VisualStudio

4. For Visual Studio 2026, locate the profile directory for the correct 18.x
   instance. Its name commonly begins with ``18.0_`` and includes an
   instance-specific suffix.
5. Open that instance's ``Extensions`` directory:

   .. code-block:: text

      %LOCALAPPDATA%\Microsoft\VisualStudio\<instance>\Extensions

6. Search the extension subdirectories for ``extension.vsixmanifest``. Open
   candidate manifests in a text editor and compare the ``Identity Id`` with
   the extension ID printed by the repair script.
7. Only after positively identifying the matching VLE extension directory,
   manually delete **that directory only**.
8. If the instance's Extensions directory contains an ``extensions.*.cache``
   file and the stale registration remains, manually delete only that cache
   file.
9. If Visual Studio still has stale MEF/editor state, manually delete
   ``ComponentModelCache`` for the **same Visual Studio instance**:

   .. code-block:: text

      %LOCALAPPDATA%\Microsoft\VisualStudio\<instance>\ComponentModelCache

   Visual Studio recreates this cache.
10. Do **not** delete the entire Visual Studio instance profile. Do **not**
    delete ``privateregistry.bin``. Do **not** delete arbitrary directories
    under Program Files. Do **not** remove the working Visual Studio 2022 copy
    when the problem is isolated to Visual Studio 2026.
11. Start Visual Studio 2026 and install VLE from **Extensions -> Manage
    Extensions -> Browse**, or rerun the repair helper with ``-InstallOnly``.

Failure cases
-------------

``vswhere.exe was not found``
    Repair or install Visual Studio Installer, or verify that ``vswhere.exe``
    exists in the standard installer directory. The script also accepts a
    ``vswhere.exe`` available on ``PATH``.

``Visual Studio is running``
    Close all Visual Studio instances. This protects the extension database and
    avoids file-lock/cache races.

``VSIXInstaller.exe is already running``
    Close the installer UI or wait for the existing operation to finish.

``No Visual Studio installation matched``
    Check ``-VersionRange``. The default intentionally ignores Visual Studio
    2022.

``Requested Visual Studio instance ID not found``
    Re-run ``vswhere``. The ID may belong to another machine, an installation
    outside the requested version range, or an installation that was removed.

``VSIX installation could not be verified``
    Inspect the most recent VSIXInstaller logs in ``%TEMP%`` and verify that the
    targeted Visual Studio installation is healthy.

``NoApplicableSKUsException``
    The supplied VSIX manifest is not applicable to the targeted Visual Studio
    product/version/architecture, or the installer is not evaluating the
    expected target. Inspect the VSIX manifest and selected instance details.

Limitations
-----------

The repair tool does not:

* Download VLE from the Marketplace.
* Change the user's Visual Studio edition.
* Install missing Visual Studio workloads/components.
* Guarantee that an arbitrary third-party VSIX supports Visual Studio 2026.
* Directly remove registry entries or profile directories.
* Modify machine-wide extensions with ``/admin``.
* Replace Visual Studio's normal Extension Manager as the preferred installer.

Related Microsoft tooling
-------------------------

* vswhere: https://github.com/microsoft/vswhere
* Finding VSIXInstaller with vswhere: https://github.com/microsoft/vswhere/wiki/Find-VSIXInstaller
* Visual Studio extension management: https://learn.microsoft.com/en-us/visualstudio/ide/finding-and-using-visual-studio-extensions
