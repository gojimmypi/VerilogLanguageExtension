echo off
:: vs-build.bat  [target] [Makefile]
:: 
:: example: vs-build.bat build/ulx3s-85k/ulx3s.bit boards/ulx3s/Makefile-ULX3S-85F.mk

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

:: depending on 32 bit or 64 bit, we'll put the path to WSL in VBUILDCMD
::
:: %windir%\Sysnative\wsl.exe for 32 bit
:: %windir%\System32\wsl.exe for 64 bit
::
IF EXIST "%windir%\Sysnative\wsl.exe" (
    SET VBUILDCMD="%windir%\Sysnative\wsl.exe"
    ) ELSE ( 
        IF EXIST "%windir%\System32\wsl.exe" (
            SET VBUILDCMD="%windir%\System32\wsl.exe"
            ) ELSE (
                SET VBUILDCMD=
                echo " WSL Not found!"
                exit 1
                )
        )   

echo Calling %VBUILDCMD% make %VBUILDTARGET% ...
%comspec% /k %VBUILDCMD% $(make -f %VBUILDMAKE% %VBUILDTARGET%)
echo Done!
