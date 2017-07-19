// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Spreads.Collections")>]
[<assembly: AssemblyProductAttribute("Spreads")>]
[<assembly: AssemblyDescriptionAttribute("Spreads")>]
[<assembly: AssemblyVersionAttribute("1.0.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0.0")>]
[<assembly: AssemblyCopyrightAttribute("(c) Victor Baybekov 2017")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.0"
