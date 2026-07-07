echo off
:: vs-clean.bat  [target] [Makefile]
::
:: example: vs-clean.bat clean boards/ulx3s/Makefile-ULX3S-85F.mk

echo Param1=%1
echo Param2=%2

if "%1" == "" (
    SET VBUILDTARGET=build/ulx3s-85k/ulx3s.bit
    ) else (
    SET VBUILDTARGET=%1
)

if "%2" == "" (
    SET VBUILDMAKE=boards/ulx3s/Makefile-ULX3S-85F.mk
    ) else (
    SET VBUILDMAKE=%2
)

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

echo Calling %VBUILDCMD% make clean ...
%VBUILDCMD% make clean -f %VBUILDMAKE% %VBUILDTARGET%
echo Done!
