%~dp0/../../tools/Bootstrapper.exe -p *blosc.dll  ./w32
%~dp0/../../tools/Bootstrapper.exe -p *blosc.dll  ./w64
%~dp0/../../tools/Bootstrapper.exe -p *blosc.so  ./l64

move /Y "%~dp0l64\libblosc.so.compressed" "%~dp0l64\libblosc.compressed"

pause