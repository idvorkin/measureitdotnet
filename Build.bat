@REM Created by the ScriptLib Utility
@echo off
setlocal
pushd %~dp0
set PATH=%WINDIR%\Microsoft.NET\Framework\v2.0.50727\;%PATH%
if /I "%1" == "/Debug" (
    MSBuild.exe /p:Configuration=Debug "MeasureIt.sln" %2 %3 %4 %5 %6 %7 %8 %9
    echo.
    echo.Created an unoptimized version of the project.
) else (
    MSBuild.exe /p:Configuration=Release "MeasureIt.sln" %2 %3 %4 %5 %6 %7 %8 %9
    echo.
    echo.A optimized version of the project was generated
    echo.Use 'build /Debug' to create an unoptimized version of the project.
)
