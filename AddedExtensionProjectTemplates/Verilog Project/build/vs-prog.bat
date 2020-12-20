echo off

if "%1" == "" ( 
    SET VBUILDMAKE=Makefile
    ) else (
    SET VBUILDMAKE=%1
)

if "%2" == "" ( 
    SET VBUILDTARGET=ULX3S
    ) else (
    SET VBUILDMAKE=%2
)

:: WSL is 64 bit and will only install on 64bit OS
:: EXCEPT Visual Studio is 32 bit. So we neeeed to detect if this is being called
:: from a 64bit command prompt, or a 32 bit Visual Studio process:
IF EXIST "%windir%\Sysnative\wsl.exe" (
  SET VBUILDCMD="%windir%\Sysnative\wsl.exe"
  ) ELSE ( 
    IF EXIST "%windir%\System32\wsl.exe" (
      SET VBUILDCMD="%windir%\System32\wsl.exe"
      ) ELSE (
        echo " WSL Not found!"
		exit 1
        )
    )
:: https://docs.microsoft.com/en-us/visualstudio/msbuild/exec-task?view=vs-2019
:: https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-well-known-item-metadata?view=vs-2019
:: https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-project-file-schema-reference?view=vs-2019
echo "Calling %VBUILDCMD% (make prog -f %VBUILDMAKE% %VBUILDTARGET%) & ..."
echo "%SystemRoot%"
:: "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\msbuild.exe"
start "Build Ping 1" /b ping -n 5 192.168.1.1
:: start "Build" /b %VBUILDCMD% (make prog -f %VBUILDMAKE% %VBUILDTARGET%) &
start "Build Ping 2" /b ping -n 5  192.168.1.2
echo "Version %3"

echo Done!