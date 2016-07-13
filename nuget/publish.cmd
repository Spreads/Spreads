@for %%f in (..\artifacts\*.nupkg) do @..\.nuget\NuGet.exe push %%f
pause