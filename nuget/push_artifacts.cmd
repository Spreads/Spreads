@echo off

dir ..\artifacts

setlocal
:PROMPT
SET /P AREYOUSURE=Push to NuGet.org? (Y/[N])?
IF /I "%AREYOUSURE%" NEQ "Y" GOTO END

@for %%f in (..\artifacts\*.nupkg) do @\tools\nuget\NuGet.exe push %%f -source https://www.nuget.org/api/v2/package
xcopy /s ..\artifacts \transient\LocalNuget

:END
endlocal

pause