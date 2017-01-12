@for %%f in (..\artifacts\*.nupkg) do @C:\tools\nuget\NuGet.exe push %%f
pause