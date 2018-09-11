@echo off
setlocal

:: Invoke VS Developer Command Prompt batch file.
:: This sets up some environment variables needed to use ILDasm and ILAsm.
if not defined VisualStudioVersion (
    if defined VS150COMNTOOLS (
        call "%VS150COMNTOOLS%\VsDevCmd.bat"
        goto :Rewrite
    )
    call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\Tools\VsDevCmd.bat"
    goto :Rewrite
    echo Error: re_write_il.cmd requires Visual Studio 2015.
    exit /b 1
)

:Rewrite
@echo on
%~dp0..\ilasm.exe /quiet /pdb /dll /key:../SpreadsKey.snk /output:out/Spreads.Unsafe.dll /nologo Spreads.Unsafe.il
nuget pack Spreads.Unsafe.nuspec -Version 1.0.5 -OutputDirectory C:/tools/LocalNuget