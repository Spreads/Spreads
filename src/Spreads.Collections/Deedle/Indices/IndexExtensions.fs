namespace Spreads.Deedle 

open Spreads.Deedle.Internal
open Spreads.Deedle.Indices
open Spreads.Deedle.Indices.Linear

// ------------------------------------------------------------------------------------------------
// F# friendly operations for creating vectors
// ------------------------------------------------------------------------------------------------

/// Set concrete IIndexBuilder implementation
///
/// [category:Vectors and indices]
[<AutoOpen>]
module ``F# IndexBuilder implementation`` =
  type IndexBuilder = 
    /// Returns concrete implementation for IVectorBuilder
    static member Instance = Spreads.Deedle.Indices.Linear.LinearIndexBuilder.Instance