@echo off
cls
If Not Exist src\.nuget\nuget.exe msbuild src\.nuget\NuGet.targets -Target:RestorePackages
If Not Exist src\packages\gitlink\lib\net45\GitLink.exe src\.nuget\nuget.exe Install gitlink -Source "https://www.nuget.org/api/v2/" -OutputDirectory "src\packages" -ExcludeVersion
If Not Exist src\packages\FAKE\tools\fake.exe src\.nuget\nuget.exe Install FAKE -Source "https://www.nuget.org/api/v2/" -OutputDirectory "src\packages" -ExcludeVersion
src\packages\FAKE\tools\fake.exe build.fsx %*
