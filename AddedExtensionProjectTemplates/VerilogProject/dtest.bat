echo off
IF EXIST "%windir%\Sysnative\wsl.exe" (
  echo "Using Sysnative\wsl"
  ) ELSE ( 
    IF EXIST "%windir%\System32\wsl.exe" (
      echo "Using System32\wsl"
      ) ELSE (
        echo " WSL Not found!"
        )
    )
