namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Period")>]
[<assembly: AssemblyProductAttribute("Spreads")>]
[<assembly: AssemblyDescriptionAttribute("Series and Panels for Reactive and Exploratory Analysis of Data Streams")>]
[<assembly: AssemblyVersionAttribute("0.0.3")>]
[<assembly: AssemblyFileVersionAttribute("0.0.3")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.3"
