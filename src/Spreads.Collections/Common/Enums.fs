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

/// Defines how IOrderedMap.Append behaves
type AppendOption =
  /// Throw if new values overlap with existing values
  | ThrowOnOverlap = 0
  /// Ignore overlap if all new key and values are equal to existing, throw on unequal keys/values
  | IgnoreEqualOverlap = 1
  /// Require that at least one (first) new value matches at least one (last) value of a map (foolproofing)
  | RequireEqualOverlap = 2
  /// Drop existing values starting from the first key of the new values and add all new values
  | DropOldOverlap = 3