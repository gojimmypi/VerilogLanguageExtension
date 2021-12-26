echo off

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
%VBUILDCMD% make clean
echo Done!