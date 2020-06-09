echo off
:: vs-build.bat  [target] [Makefile]
:: 
:: example: vs-build.bat ULX3S Makefile-ULX3S-12F.mk

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

echo Calling %VBUILDCMD% make ulx3s.bit ...
%comspec% /k %VBUILDCMD% $(make -f %VBUILDMAKE% %VBUILDTARGET%)
echo Done!
