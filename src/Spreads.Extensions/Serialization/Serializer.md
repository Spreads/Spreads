(TODO Update this, it is a though dump and is not relevant now)

Spreads.Serializer
------------------

Spreads.Serializer is ultra-fast and efficient binary serializer. It provides high compression for arrays of blittable types
and supports any other type via JSON.NET's BSON serializer.

For single primitive and known blittable types, Spreads.Serializer returns their native representation in bytes. This differs from
BSON specification, which only supports keyed root elements and doesn't support serialization of a single number.

For unknown value types that cannot be pinned in memory, the Serializer uses `Marshal.StructureToPtr`/`Marshal.PtrToStructure` to get 
bytes representation. (**Attention!** This could have unexpected behavior, e.g. see [this](http://stackoverflow.com/questions/31293457/marshal-structuretoptr-ptrtostructure-rounds-datetime-field) SO question. 
Try to make your custom structures blittable and pinnable whenever possible.)

The Serializer returns null for null any null reference type. For empty array, there is no standard native representation, therefore 
we use zero byte to indicate non-null empty array. Serialized non-empty arrays are prefixed with a single byte indicating their value
type.

Supported types and their prefixes correspond to [`TypeCode` enumeration](https://msdn.microsoft.com/en-us/library/system.typecode(v=vs.110).aspx) 
with slight changes in empty meaning and with some additional types:

**Array storage types:**

* Empty = 0,          // Empty array of any type
* Object = 1,         // Instance that isn't a value
* DBNull = 2,         // Database null value
* Boolean = 3,        // Boolean
* Char = 4,           // Unicode character
* SByte = 5,          // Signed 8-bit integer
* Byte = 6,           // Unsigned 8-bit integer
* Int16 = 7,          // Signed 16-bit integer
* UInt16 = 8,         // Unsigned 16-bit integer
* Int32 = 9,          // Signed 32-bit integer
* UInt32 = 10,        // Unsigned 32-bit integer
* Int64 = 11,         // Signed 64-bit integer
* UInt64 = 12,        // Unsigned 64-bit integer
* Single = 13,        // IEEE 32-bit float
* Double = 14,        // IEEE 64-bit double
* Decimal = 15,       // Decimal
* DateTime = 16,      // DateTime
* **TimeSpan** = 17,      // TimeSpan, see http://stackoverflow.com/questions/7329834/what-happened-to-system-typecode-of-value-17
* String = 18,        // Unicode character string

TODO Tick, Candle(?)

Other known types that are converted to one of the storage types or their combination:
* 
* FloatTick - 

For types that are not in the list but could be pre


