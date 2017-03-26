@echo off
setlocal

:: Invoke VS Developer Command Prompt batch file.
:: This sets up some environment variables needed to use ILDasm and ILAsm.
if not defined VisualStudioVersion (
    if defined VS150COMNTOOLS (
        call "%VS150COMNTOOLS%\VsDevCmd.bat"
        goto :Rewrite
    )

    echo Error: re_write_il.cmd requires Visual Studio 2015.
    exit /b 1
)

:Rewrite
@echo on
%~dp0\ildasm.exe /caverbal /linenum /out:..\bin\net451\spreads.core.beforerewrite.il /nobar ..\bin\net451\Spreads.Core.dll
%~dp0\ILSub\ILSub.exe ..\bin\net451\spreads.core.beforerewrite.il ..\bin\net451\spreads.core.rewritten.il
%~dp0\ilasm.exe /quiet /pdb /dll /output:..\bin\net451\Spreads.Core.dll /nologo /key=SpreadsKey.snk ..\bin\net451\spreads.core.rewritten.il 