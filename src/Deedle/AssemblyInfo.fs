namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Deedle")>]
[<assembly: AssemblyProductAttribute("Spreads")>]
[<assembly: AssemblyDescriptionAttribute("Spreads")>]
[<assembly: AssemblyVersionAttribute("0.0.44")>]
[<assembly: AssemblyFileVersionAttribute("0.0.44")>]
[<assembly: AssemblyCopyrightAttribute("(c) Victor Baybekov 2015")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.44"
