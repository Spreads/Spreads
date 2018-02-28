@for %%f in (..\artifacts\*.nupkg) do @C:\tools\nuget\NuGet.exe push %%f -source https://www.nuget.org/api/v2/package
pause