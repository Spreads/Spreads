# Spreads library introduction

TBD




## Performance

When working with blittable data Spreads performane is limited by (de)serialization and, if data is already in memory, by interface method call.
(This does not account for user calculations and related to Spreads overhead.)

We try to delay despecialization from generics to interfaces as much as possible. However, in certain cases (generic operators) this is not possible.

You could use operators and unspecialized methods for quick prototyping, but if method calls turn out to be a problem you could replace operators 
with generic Zip overloads that accept strongly-typed operatoins type as write your custom specialized operation (TODO reword, example, implement it actually).


### How to get the best performance

#### Use blittable `struct` for values instead of `class`. 

* Even `string`s could be replaced by `factor` (TBD).
* Blittable structs are serialized to/from JSON very fast. Use custom formatter to speedup serialization of your value type. (TODO links here).
* Do not use strings when they could be interpolated from blittable data. Instead use map projection (and ZeroFormatter). 
Additional benefit is that you could change the output string very easily, e.g. format or language.
* Do not use stings to save highest precision possible. `Decimal` should be enough for everyone. (TODO x-plat decimal or int128/256 blittable BigDecimal)

