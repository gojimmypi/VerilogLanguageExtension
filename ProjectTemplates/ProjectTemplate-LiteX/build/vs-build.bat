echo off
:: vs-build.bat [Makefile] [target]
:: 
:: example: vs-build.bat Makefile-ULX3S-12F.mk

if "%1" == "" ( 
    SET VBUILDMAKE=Makefile
    ) else (
    SET VBUILDMAKE=%1
)

if "%1" == "" ( 
    SET VBUILDTARGET=ULX3S
    ) else (
    SET VBUILDMAKE=%2
)


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
echo %VBUILDCMD% /mnt/c/workspace/ulx3s-toolchain/soft_cpu.sh
echo Done!