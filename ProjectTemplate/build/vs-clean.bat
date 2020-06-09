echo off
:: vs-clean.bat  [target] [Makefile]
:: 
:: example: vs-clean.bat ULX3S Makefile-ULX3S-12F.mk

echo Param1=%1
echo Param2=%2

if "%1" == "" ( 
    SET VBUILDTARGET=ULX3S
    ) else (
    SET VBUILDTARGET=%1
)

if "%2" == "" ( 
    SET VBUILDMAKE=Makefile
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