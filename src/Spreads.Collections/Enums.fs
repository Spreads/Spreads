namespace Spreads
open System

/// Lookup direction on sorted maps
type Lookup =
    /// Less than
    | LT = -2
    /// Less or equal
    | LE = -1
    /// Exactly equal
    | EQ = 0
    /// Greater or equal
    | GE = 1
    /// Greater than
    | GT = 2