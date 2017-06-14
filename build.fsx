// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"C:/tools/BUILD/FAKE/FakeLib.dll"

open Fake
open Fake.Git
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
let tags = "Spreads data streaming real-time analysis streams time series reactive"

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

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    let packageName = project + "." + "Utils"
    //NuGet (fun p ->
    //    { p with
    //        Authors = authors
    //        Project = packageName
    //        Summary = packageName // "TODO"
    //        Description = packageName // "TODO"
    //        Version = "0.9.0"
    //        ReleaseNotes = ""
    //        Tags = tags
    //        OutputPath = "C:/tools/LocalNuget/"
    //        AccessKey = getBuildParamOrDefault "nugetkey" ""
    //        Publish = hasBuildParam "nugetkey"
    //        Dependencies = [  ]
    //           })
    //    ("nuget/" + packageName + ".nuspec")
    let packageName = project + "." + "Unsafe"
    NuGet (fun p ->
        { p with
            Authors = authors
            Project = packageName
            Summary = packageName // "TODO"
            Description = packageName // "TODO"
            Version = "1.0.0"
            ReleaseNotes = ""
            Tags = tags
            OutputPath = "C:/tools/LocalNuget/"
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [  ]
               })
        ("nuget/" + packageName + ".nuspec")

)

Target "Pack" DoNothing
 
"NuGet"  ==> "Pack"

RunTargetOrDefault "NuGet"
