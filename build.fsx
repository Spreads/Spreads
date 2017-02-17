// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"C:/tools/BUILD/FAKE/FakeLib.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO


// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Spreads"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Spreads" //"Series and Panels for Reactive and Exploratory Analysis of Data Streams"

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "Spreads" //"Series and Panels for Reactive and Exploratory Analysis of Data Streams"

// List of author names (for NuGet package)
let authors = [ "Victor Baybekov" ]

// Tags for your project (for NuGet package)
let tags = "Spreads F# data streaming real-time analysis streams time series reactive"

// File system information 
let solutionFile  = "Spreads.sln"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "Spreads"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "Spreads"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/Spreads"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release = LoadReleaseNotes "RELEASE_NOTES.md"

let genFSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName
    let fileName = basePath + "/AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion
        Attribute.Copyright "(c) Victor Baybekov 2015" ]

let genCSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName + "/Properties"
    let fileName = basePath + "/AssemblyInfo.cs"
    CreateCSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion
        Attribute.Copyright "(c) Victor Baybekov 2015" ]

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
  let fsProjs =  !! "src/**/*.fsproj"
  let csProjs = !! "src/**/*.csproj"
  fsProjs |> Seq.iter genFSAssemblyInfo
  csProjs |> Seq.iter genCSAssemblyInfo
)

Target "RestorePackages" RestorePackages

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

Target "CompressResourceDlls" (fun _ ->
    !! solutionFile
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)


// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    !! solutionFile
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    !! testAssemblies
    |> NUnit (fun p ->
        { p with
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->

    let packageName = project + "." + "Utils"
    NuGet (fun p ->
        { p with
            Authors = authors
            Project = packageName
            Summary = packageName // "TODO"
            Description = packageName // "TODO"
            Version = "0.7.0"
            ReleaseNotes = ""
            Tags = tags
            OutputPath = "C:/tools/LocalNuget/"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [  ]
               })
        ("nuget/" + packageName + ".nuspec")

//    let packageName = project + "." + "Extensions"
//    NuGet (fun p ->
//        { p with
//            Authors = authors
//            Project = packageName
//            Summary = packageName // "TODO"
//            Description = packageName // "TODO"
//            Version = release.NugetVersion
//            ReleaseNotes = ""
//            Tags = tags
//            OutputPath = "bin"
//            AccessKey = getBuildParamOrDefault "nugetkey" ""
//            Publish = hasBuildParam "nugetkey"
//            Dependencies = 
//              [ "Spreads.Core", release.NugetVersion
//                "Newtonsoft.Json", GetPackageVersion "packages" "Newtonsoft.Json"
//                "NodaTime", GetPackageVersion "packages" "NodaTime"
//                ]
//            })
//        ("nuget/" + packageName + ".nuspec")
//
//    let packageName = project
//    NuGet (fun p ->
//        { p with
//            Authors = authors
//            Project = packageName
//            Summary = packageName // "TODO"
//            Description = packageName // "TODO"
//            Version = release.NugetVersion
//            ReleaseNotes = ""
//            Tags = tags
//            OutputPath = "bin"
//            AccessKey = getBuildParamOrDefault "nugetkey" ""
//            Publish = hasBuildParam "nugetkey"
//            Dependencies = 
//              [ "Spreads.Core", release.NugetVersion
//                "Spreads.Extensions", release.NugetVersion
//                ]
//            })
//        ("nuget/" + packageName + ".nuspec")

//    let packageName = project + "." + "RPlugin"
//    NuGet (fun p ->
//        { p with
//            Authors = authors
//            Project = packageName
//            Summary = packageName // "TODO"
//            Description = packageName // "TODO"
//            Version = release.NugetVersion
//            ReleaseNotes = ""
//            Tags = tags
//            OutputPath = "bin"
//            AccessKey = getBuildParamOrDefault "nugetkey" ""
//            Publish = hasBuildParam "nugetkey"
//            Dependencies = 
//              [ "Spreads", release.NugetVersion
//                "R.NET.Community", GetPackageVersion "packages" "R.NET.Community"
//                "R.NET.Community.FSharp", GetPackageVersion "packages" "R.NET.Community.FSharp"
//                "RProvider", GetPackageVersion "packages" "RProvider" ]
//            })
//        ("nuget/" + packageName + ".nuspec")
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateReferenceDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"] [] then
      failwith "generating reference documentation failed"
)

let generateHelp fail =
    if executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:HELP"] [] then
        traceImportant "Help generated"
    else
        if fail then
            failwith "generating help documentation failed"
        else
            traceImportant "generating help documentation failed"
    

Target "GenerateHelp" (fun _ ->
    DeleteFile "docs/content/release-notes.md"    
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp true
)


Target "KeepRunning" (fun _ ->    
    use watcher = new FileSystemWatcher(DirectoryInfo("docs/content").FullName,"*.*")
    watcher.EnableRaisingEvents <- true
    watcher.Changed.Add(fun e -> generateHelp false)
    watcher.Created.Add(fun e -> generateHelp false)
    watcher.Renamed.Add(fun e -> generateHelp false)
    watcher.Deleted.Add(fun e -> generateHelp false)

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.EnableRaisingEvents <- false
    watcher.Dispose()
)

Target "GenerateDocs" DoNothing

// --------------------------------------------------------------------------------------
// Release Scripts

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    fullclean tempDocsDir
    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

//#load "paket-files/fsharp/FAKE/modules/Octokit/Octokit.fsx"
//open Octokit

Target "Release" (fun _ ->
    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
    
    // release on github
//    createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
//    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes 
//    // TODO: |> uploadFile "PATH_TO_FILE"    
//    |> releaseDraft
//    |> Async.RunSynchronously
)

Target "Pack" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "RestorePackages"
  //==> "AssemblyInfo"
  ==> "Build"
  ==> "All"
  ==> "RunTests"
  =?> ("GenerateReferenceDocs",isLocalBuild && not isMono)
  =?> ("GenerateDocs",isLocalBuild && not isMono)
  =?> ("ReleaseDocs",isLocalBuild && not isMono)

//"All" 
//  //==> "RunTests"
//  ==> 
"AssemblyInfo"
  ==> "NuGet"
  ==> "Pack"

"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"GenerateHelp"
  ==> "KeepRunning"
    
"ReleaseDocs"
  ==> "Release"

"Pack"
  ==> "Release"

RunTargetOrDefault "NuGet"
