namespace Spreads

open System
open System.Collections
open System.Collections.Generic
open System.Diagnostics
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Spreads

[<AbstractClassAttribute>]
[<AllowNullLiteral>]
[<SerializableAttribute>]
type Panel<'TRowKey,'TColumnKey, 'TValue>() =
  inherit Series<'TRowKey, Series<'TColumnKey, 'TValue>>()



// ColumnsPanel uses ZipN cursor over source columns
// RowsPanel has metarialized rows