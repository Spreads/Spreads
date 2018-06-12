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
%~dp0\ildasm.exe /caverbal /linenum /out:%1Spreads.Collections.beforerewrite.il /nobar %2
%~dp0\ILSub\ILSub.exe %1Spreads.Collections.beforerewrite.il %1Spreads.Collections.rewritten.il
%~dp0\ilasm.exe /quiet /pdb /dll /key:%3 /output:%2 /nologo %1Spreads.Collections.rewritten.il