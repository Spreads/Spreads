// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Spreads.Collections")>]
[<assembly: AssemblyProductAttribute("Spreads")>]
[<assembly: AssemblyDescriptionAttribute("Spreads")>]
[<assembly: AssemblyVersionAttribute("0.6.0-beta2")>]
[<assembly: AssemblyFileVersionAttribute("0.6.0-beta2")>]
[<assembly: AssemblyCopyrightAttribute("(c) Victor Baybekov 2016")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.6.0-beta2"
