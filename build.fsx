#r @"src/packages/FAKE/tools/FakeLib.dll"
open System.IO
open Fake
open Fake.AssemblyInfoFile
open Fake.Git.Information
open Fake.SemVerHelper
open System

let buildOutputPath = "./build_output"
let buildArtifactPath = "./build_artifacts"
let nugetWorkingPath = FullName "./build_temp"
let packagesPath = FullName "./src/packages"


let assemblyVersion = "1.0.0"
let baseVersion = "1.0.0"

let semVersion : SemVerInfo = parse baseVersion

let Version = semVersion.ToString()

let branch = (fun _ ->
  (environVarOrDefault "APPVEYOR_REPO_BRANCH" (getBranchName "."))
)

let FileVersion = (environVarOrDefault "APPVEYOR_BUILD_VERSION" (Version + "." + "0"))

let informationalVersion = (fun _ ->
  let branchName = (branch ".")
  let label = if branchName="master" then "" else " (" + branchName + "/" + (getCurrentSHA1 ".").[0..7] + ")"
  (FileVersion + label)
)

let nugetVersion = (fun _ ->
  let branchName = (branch ".")
  let label = if branchName="master" then "" else "-" + (if branchName="mt3" then "beta" else branchName)
  (FileVersion + label)
)

let InfoVersion = informationalVersion()
let NuGetVersion = nugetVersion()


printfn "Using version: %s" Version
printfn "NugetVersion: %s" NuGetVersion

Target "Clean" (fun _ ->
  ensureDirectory buildOutputPath
  ensureDirectory buildArtifactPath
  ensureDirectory nugetWorkingPath

  CleanDir buildOutputPath
  CleanDir buildArtifactPath
  CleanDir nugetWorkingPath
)

Target "RestorePackages" (fun _ -> 
     "./src/MassTransit.RedisSagas.sln"
     |> RestoreMSSolutionPackages (fun p ->
         { p with
             OutputPath = packagesPath
             Retries = 4 })
)

Target "Build" (fun _ ->

  CreateCSharpAssemblyInfo @".\src\SolutionVersion.cs"
    [ Attribute.Title "MassTransit.RedisSagas"
      Attribute.Description "MassTransit.RedisSagas is a distributed application framework for .NET https://github.com/Royal-Jay/MassTransit.RedisSagas"
      Attribute.Product "MassTransit.RedisSagas"
      Attribute.Version assemblyVersion
      Attribute.FileVersion FileVersion
      Attribute.InformationalVersion InfoVersion
    ]

  let buildMode = getBuildParamOrDefault "buildMode" "Release"
  let setParams defaults = { 
    defaults with
        Verbosity = Some(Quiet)
        Targets = ["Clean"; "Build"]
        Properties =
            [
                "Optimize", "True"
                "DebugSymbols", "True"
                "RestorePackages", "True"
                "Configuration", buildMode
                "TargetFrameworkVersion", "v4.6.1"
                "Platform", "Any CPU"
            ]
  }

  build setParams @".\src\MassTransit.RedisSagas.sln"
      |> DoNothing
)

let gitLink = (packagesPath @@ "gitlink" @@ "build" @@ "GitLink.exe")

Target "GitLink" (fun _ ->

    if String.IsNullOrWhiteSpace(gitLink) then failwith "Couldn't find GitLink.exe in the packages folder"

    let pdb = (__SOURCE_DIRECTORY__ @@ "src" @@ "MassTransit.RedisSagas" @@ "bin" @@ "Release" @@ "MassTransit.RedisSagas.pdb")
    let ok =
        execProcess (fun info ->
            info.FileName <- gitLink
            info.Arguments <- (sprintf "%s -u https://github.com/Royal-Jay/MassTransit.RedisSagas" pdb)) (TimeSpan.FromSeconds 30.0)
    if not ok then failwith (sprintf "GitLink.exe %s' task failed" pdb)

)


let testDlls = [ "./src/MassTransit.RedisSagas.Tests/bin/Release/MassTransit.RedisSagas.Tests.dll" ]

Target "UnitTests" (fun _ ->
    testDlls
        |> NUnit (fun p -> 
            {p with
                Framework = "v4.0.30319"
                DisableShadowCopy = true; 
                OutputFile = buildArtifactPath + "/nunit-test-results.xml"})
)

type packageInfo = {
    Project: string
    PackageFile: string
    Summary: string
    Files: list<string*string option*string option>
}

Target "Package" (fun _ ->

  let nugs = [| { Project = "MassTransit.RedisSagas"
                  Summary = "MassTransit RedisSagas, a persistence store for MassTransit using Redis"
                  PackageFile = @".\src\MassTransit.RedisSagas\packages.config"
                  Files = [ (@"..\src\MassTransit.RedisSagas\bin\Release\MassTransit.RedisSagas.*", Some @"lib\net46", None);
                            (@"..\src\MassTransit.RedisSagas\**\*.cs", Some "src", None) ] }
                { Project = "MassTransit.RedisSagas.StrongName"
                  Summary = "MassTransit RedisSagas (Strong Name), a persistence store for MassTransit using Redis"
                  PackageFile = @".\src\MassTransit.RedisSagas.StrongName\packages.config"
                  Files = [ (@"..\src\MassTransit.RedisSagas.StrongName\bin\Release\MassTransit.RedisSagas.*", Some @"lib\net46", None);
                            (@"..\src\MassTransit.RedisSagas.StrongName\**\*.cs", Some "src", None) ] }
                { Project = "MassTransit.RedisSagas.RedLock"
                  Summary = "MassTransit RedisSagas.RedLock, a persistence store for MassTransit using Redis and distributed locking"
                  PackageFile = @".\src\MassTransit.RedisSagas.RedLock\packages.config"
                  Files = [ (@"..\src\MassTransit.RedisSagas.RedLock\bin\Release\MassTransit.RedisSagas.RedLock.*", Some @"lib\net46", None);
                            (@"..\src\MassTransit.RedisSagas.RedLock\**\*.cs", Some "src", None) ] }
                { Project = "MassTransit.RedisSagas.RedLock.StrongName"
                  Summary = "MassTransit RedisSagas.RedLock (Strong Name), a persistence store for MassTransit using Redis and distributed locking"
                  PackageFile = @".\src\MassTransit.RedisSagas.RedLock.StrongName\packages.config"
                  Files = [ (@"..\src\MassTransit.RedisSagas.RedLock.StrongName\bin\Release\MassTransit.RedisSagas.RedLock.StrongName*", Some @"lib\net46", None);
                            (@"..\src\MassTransit.RedisSagas.RedLock.StrongName\**\*.cs", Some "src", None) ] }
             |]

  nugs
    |> Array.iter (fun nug ->

      let getDeps daNug : NugetDependencies =
        if daNug.Project = "MassTransit.RedisSagas" then (getDependencies daNug.PackageFile)
        else if daNug.Project = "MassTransit.RedisSagas.StrongName" then (getDependencies daNug.PackageFile) 
        else if daNug.Project = "MassTransit.RedisSagas.RedLock" then (getDependencies daNug.PackageFile) 
        else if daNug.Project = "MassTransit.RedisSagas.RedLock.StrongName" then (getDependencies daNug.PackageFile) 
        else ("MassTransit.RedisSagas", NuGetVersion) :: (getDependencies daNug.PackageFile)

      let setParams defaults = {
        defaults with 
          Authors = ["Royal Jay" ]
          Description = "MassTransit.RedisSagas is a distributed application framework for .NET"
          OutputPath = buildArtifactPath
          Project = nug.Project
          Dependencies = (getDeps nug)
          Summary = nug.Summary
          SymbolPackage = NugetSymbolPackage.Nuspec
          Version = NuGetVersion
          WorkingDir = nugetWorkingPath
          Files = nug.Files
      } 

      NuGet setParams (FullName "./template.nuspec")
    )
)

Target "Default" (fun _ ->
  trace "Build starting..."
)

"Clean"
  ==> "RestorePackages"
  ==> "Build"
  ==> "GitLink"
  ==> "Package"
  ==> "Default"

RunTargetOrDefault "Default"