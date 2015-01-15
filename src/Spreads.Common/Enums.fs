namespace Spreads

// TODO (?) rename to Deedle's name and then use D's Lookup directly
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


//  Deedle.Lookup Definition:
//[<System.Flags ()>]
//type Lookup =
//  /// Lookup a value associated with the exact specified key. If the key is not available, then fail or return missing value.
//  | Exact = 1
//  /// Lookup a value associated with the specified key or with the nearest greater key that has a value available. Fails (or returns missing value) only when the specified key is greater than all available keys.
//  | ExactOrGreater = 3
//  /// Lookup a value associated with the specified key or with the nearest smaller key that has a value available. Fails (or returns missing value) only when the specified key is smaller than all available keys.
//  | ExactOrSmaller = 5
//  /// Lookup a value associated with a key that is greater than the specified one.  Fails (or returns missing value) when the specified key is greater or equal to the greatest available key.
//  | Greater = 2
//  /// Lookup a value associated with a key that is smaller than the specified one.  Fails (or returns missing value) when the specified key is smaller or equal to the smallest available key.
//  | Smaller = 4





// Infix TODOs
// TODO1 recheck this claim: Used for optimized zip/join. DefaultBehavior uses general Combination 
// algorithm which defaults to Continuous, making opeartion O(2*n*log n)
// TODO2 make JB a part of ISeries (for sorted maps Inner implied, LINQ join in IE<KVP<_,_>> is enough)
// TODO3 how JB will help working with combinations and panels? Could we make a generalized Combination
// not just a zip as fast as joining two ISeries?
(*      
    1. Select all first keys (MoveFirst on each) into a sorted hashset
    2. For each key get values into an array/indexed map.
        2.1. If any value is not available - discard the row for that key
        2.2. Process row with given transformation function
        2.3. Move 

    with IB we avoid implicitly moving window pointer by calling .Item
    we should't have done this in the first place (that is, GetPointer of Window should be an object expression!)
    but with IB we also could avoid lookups if we moved ahead of keys
    we must keep previous row values so if MoveNext for rp(x) goes from 1 to 3 but we are evaluating
    a row for the key 2 then we could know that we could use x from row 1 without any additional lookups for x
 
*)

/// Options how to substitute missing values and resolve conflicts in zip/join operations for series.
type MissingBehavior =
/// A missing value is not substituted by anything (inner join). This is the default behavior.
| Exact = 0
/// Use value from the previous key. Same as Lookup.LE.
| Repeat = 1
/// Use value from the next key. Same as Lookup.GE.
| RepeatBack = 2
/// Use generic zero value
| FillZero = 3
/// Use generic one value
| FillOne = 4
/// Use generic missing value
| FillMissing = 5
/// Fill with a specific value
| FillValue = 6
/// Call .Item.[x] for a missing key x is required.
/// E.g. for interpolation or curve models values are calculated for all possible keys.
| Continuous = 7

///// Use value with priority if both values are available and we need only one value. If both sides have a priority 
///// then use default prioriry (usually left over right).
///// Note that all other InfixBehavior options by their meaning give priority to other parts since they depend on others' keys.
//| Priority // NB this is wrong abstraction, because we are making a row of several columns' value, not a value here