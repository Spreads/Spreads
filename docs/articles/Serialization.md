# Serialization

Serialization - when required - is usually a bottleneck in Spreads and DataSpreads
since all other parts are pretty fast. Therefore serialization has special attention.
We have a custom serialization format that supports binary or JSON
serialization and [Zstandard](https://github.com/facebook/zstd)/[LZ4](https://github.com/lz4/lz4)/
[Gzip](https://en.wikipedia.org/wiki/Gzip) compression.

Binary format in general means blittable (or unmanaged) structures or composites/containers of those are
directly written/read to/from a binary buffer. Binary serializer is usually an order of
magnitude faster than even the fastest JSON serializer. However a data type must meet
special requirements to use the binary format and
cross-language portability could be limited (e.g. consuming binary data from JavaScript).

JSON format is the most versatile one and works everywhere, but it is usually slower than
most binary formats. We use JSON as the default format unless you chose the binary format
explicitly. But even in that case we fallback to JSON if a data type is not compatible with
our binary serialization.

Our JSON serializer [Spreads.Utf8Json](https://github.com/Spreads/Utf8Json) is very fast and
does not allocate memory. It should be suitable for most real world applications that require
data portability and have many IO operations (e.g. web applications). On some benchmarks it
is even faster than `protobuf-net` and could write/read many millions of simple data structures
per second. But if this is not enough you could get 10x better performance by using our built-in
support for binary serialization of primitives and tuples. Or you could design your
data structures in a [special way](#Fixed-size-types) that makes them compatible with our binary format.

## Binary layout of serialized data

Both binary and JSON formats have a 4-bytes header that describes the content of
payload and optional 8-bytes timestamp. Then serialized payload length is added
for variable size data.

Payload is a UTF8 JSON or custom binary, depending on format specified in the header.

```'ini
0                   1                   2                   3
0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                        DataTypeHeader                         |
+---------------------------------------------------------------+
|                Optional Timestamp (part 1)                    |
+---------------------------------------------------------------+
|                Optional Timestamp (part 2)                    |
+---------------------------------------------------------------+
|            Optional Variable size payload length              |
+---------------------------------------------------------------+
|                           Payload                             |
+---------------------------------------------------------------+
|                             ...                               |

```

### DataTypeHeader

`DataTypeHeader` has the following layout:

```'ini
0                   1                   2                   3
0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
| Version+Flags |     TEOFS     |     TEOFS1    |     TEOFS2    |
+---------------------------------------------------------------+
```

`Version+Flags` slot contains information about serialization format:

```'ini
0 1 2 3 4 5 6 7 8
+-+-+-+-+-+-+-+-+
|0|Ver|R|T|CMP|B|
+---------------+
B - Binary format (read as "Not JSON"). If not set then the payload is JSON, if set then payload is blittable or custom binary.
CMP - compression method:
    00 - not compressed
    01 - GZip
    10 - Lz4
    11 - Zstd
T - Timestamped. A value has Timestamp (8 bytes) right after the header.
R - reserved.
Ver - `IBinarySerializer<T>.SerializerVersion`.
0 - will need completely new layout when this is not zero.
```

`TEOFS/TEOFS1/TEOFS2` slots contain type information. TEOFS stands for TypeEnumOrFixedSize.
These 3 slot allow to describe most scalar and composite data types and container shapes.
They are also enough to calculate the size of any supported fixed-size type.

Describing all type enumerations and slots will take too much space. Please see 
the source code of [TypeEnum](TODO), [TypeEnumOrFixedSize](TODO) and [DataTypeHeader](TODO). There are detailed comments on the header slots content and size calculation logic
for composites and containers.

## Binary serialization

Our built-in binary serialization works only with fixed size types or 
composites/containers with such types.

You could also provide an implementation of `IBinarySerializer<T>` interface
for your type `T` that is not fixed but you do not want to use JSON fallback for performance
or other reasons. For example, you could use FlatBuffers inside the interface implementation. 
See [details below](#`IBinarySerializer<T>`-interface).

### Fixed size types

Fixed size types are fixed scalar types and some composite types made of all fixed scalar types.

Fixed scalar types are:

* Primitive types (e.g. `int32`, `double`);
* Well-known blittable structures from .NET or Spreads (e.g. `DateTime`, `decimal`, [`Symbol`](http://docs.dataspreads.io/spreads/api/Spreads.DataTypes.Symbol.html), [`UUID`](http://docs.dataspreads.io/spreads/api/Spreads.DataTypes.UUID.html));
* Custom user `struct`s that are blittable, tightly packed and have [`BinarySerializationAttribute`](http://docs.dataspreads.io/spreads/api/Spreads.Serialization.BinarySerializationAttribute.html)
with `blittableSize` parameter set to actual size (this is used as opt-in to treat a structure as blittable).
See [details below](#Custom-fixed-size-structures).

Fixed composite types are:

* [`ValueTuple<>`](https://docs.microsoft.com/en-us/dotnet/api/system.valuetuple-1)s, [`Tuple<>`](https://docs.microsoft.com/en-us/dotnet/api/system.tuple-1)s with 1 to 8
items when each item is a fixed scalar type;
* [`KeyValuePair<TKey,TValue>`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.keyvaluepair-2) is treated as [`ValueTuple<TKey,TValue>`] for serialization;
* Fixed-length arrays of fixed scalar types;

> [!NOTE]
> At the moment tuples of tuples cannot be treated as fixed size types even if all items are fixed size scalars, but we plan to add support for that. As a part of that work we also plan to treat custom user structures as tuples, making them interoperable and portable (TODO).

### Custom fixed size structures

Custom user-defined structures could be treated as fixed size ones if they are blittable.
Blittable is the most important concept for our binary serialization.
[Blittable types](https://en.wikipedia.org/wiki/Blittable_types) are data types that have
an identical presentation in memory for both managed and unmanaged code.

A blittable structure is considered as fixed size by our binary serializer if:

1. It has [`BinarySerializationAttribute`](http://docs.dataspreads.io/spreads/api/Spreads.Serialization.BinarySerializationAttribute.html)
with `blittableSize` parameter set to actual size. This is used as explicit opt-in to treat a structure as blittable.

2. It is *tightly packed*, which means that a structure does not have
any padding automatically added by a compiler. Use [`StructLayoutAttribute`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute) with the [`Pack`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.structlayoutattribute.pack) property set to `1`.
This is used as a check to ensure that a user controls every byte of the layout. You may add padding manually if you need it.

> [!WARNING]
> If the provided `blittableSize` is not equal to a runtime calculated size then your application will crash during the first attempt to serialize a structure. This means that you struct layout definition is incorrect and data corruption is possible, so we fail fast.
> [!WARNING]
> Custom blittable user structs are not versionable. You cannot change a struct layout (even cannot add new members at the end) and use it to read existing data. Design your struct very carefully and consider future changes. You may add reserved unused fields -
they will add little overhead but will be compressed away completely if you use
compression, especially with byte shuffling. Spreads always shuffles and compresses
arrays of fixed size types and the only impact of unused padding in structures is higher memory usage of such structures when they are in memory.


### `IBinarySerializer<T>` interface

The [`IBinarySerializer<T>`](http://docs.dataspreads.io/spreads/api/Spreads.Serialization.IBinarySerializer-1.html) interface allows you to provide a custom logic for binary serialization.
Our built-in serialization of composite types and containers is done via
this interface and you could use our source code as a reference.

> [!NOTE]
> You could implement this interface for a blittable structure but then you should not
> opt-in to treat it as fixed size, i.e. do not provide `blittableSize` parameter
> to [`BinarySerializationAttribute`]. These are two explicit and conflicting intents
> indicated by a user and Spreads will fail fast because it is impossible to choose one.

TODO T[] currently falls back to json, we could either implement binary serialization
of T[] when `IBinarySerializer<T>` is present or require `IBinarySerializer<T[]>`.
But it looks like the more logical and intuitive way is to serialize each element as binary but use `IBinarySerializer<T[]>` if present - this is user intent to treat `T[]`
specially and we must honor it in any case. Review if we already keep provided instance
and do not override it with `T[]`.

TODO how to register the interface

TODO link to an example below

## JSON serialization

For simple objects and structures you usually do not have to anything for JSON serialization to work. Simple means an object/struct has only public fields
or auto properties.

If you need to customize JSON serialization behavior
please go to the original [Utf8Json documentation](https://github.com/neuecc/Utf8Json). High-level API is the same in our fork.

TODO

### JSON tuples

Tuples are serialized as JSON arrays. This is different from the original Utf8Json implementation but follows TypeScript definition of tuples. A tuple
by definition is an *ordered* sequence of elements
and in JSON only arrays have order of elements, while
fields of objects could be serialized in random order.

### JSON formatters for custom fixed size struct

If you use [custom fixed size structures](#Custom-fixed-size-structures) we highly recommend that you also define a custom JSON formatter that serializes that struct as if it was a tuple. Because the structure is already set in stone it's members
order and composition should not change, therefore
serializing as a JSON array will not lose any information. At the same time serializing as JSON 
array is much faster.

> [!NOTE]
> In the future custom fixed non-union structures will be automatically serialized to/from JSON as arrays of its members.

## Compression

### Shuffle/unshuffle

TODO

### Settings

TODO Min compression len, levels with defaults

## Examples

TODO example of fixed size custom struct with json formatter
TODO example of IBinarySerializer interface

## FAQ

**Q: Why Timestamp is special and not a part of data?**

In DataSpreads we timestamp all data stream items (events) and use timestamps for quick navigation (such an index in DB), therefore
fixed explicit position is required. If you do not provide a
timestamp DataSpreads will use current time of writing.
Outside DataSpreads context you could just ignore it.

**Q: Why you are not using *Protocol buffers* for binary serialization?**

Spreads.Utf8Json is already faster than `protobuf-net` on some benchmarks
and in general has the same order of magnitude performance. JSON as data format
does not require schema and is much more portable across platforms and languages.

**Q: Why you are not using *MessagePack* for binary serialization?**

For the same reason as we are not using *Protocol buffers*.
While Spreads.Utf8Json is not yet faster than MessagePack the performance
is of the same order of magnitude. Any existing schemaless binary 
format will not give a 10x speed compared to blittable writes/reads.

**Q: Why you are not using *FlatBuffers* for binary serialization?**

*FlatBuffers* format requires schema and is not suitable for generic
serialization. However nothing stops you from using *FlatBuffers* in
an implementation of [`IBinarySerializer`](#`IBinarySerializer<T>`-interface) 
for your type. This is exactly the purpose of that interface to allow using
any existing serialization tool that require a schema on a per-type basis.

**Q: Isn't the header overhead too big for simple small values?**

No. You could use a separate header location that is shared by many values. In that case
all writes check that a header for a new value matches the existing header.

**Q: How to ensure item alignment of a serialized sequence of fixed size type?**

If you use a separate data header destination (e.g. the first 4 bytes of a large destination buffer) and start writing from aligned position then data is aligned (vs destination start) to 8 bytes if you use timestamps or to data item size without timestamps. If you need timestamps and need more than 8 bytes alignment then
pad you structure so that is has size `Pow2 - 8` to place timestamps
at the beginning of Pow2 spans.

**Q: Should I care about aligned memory access?**

We believe no! Just use `Unsafe.ReadUnaligned` if you are doing low-level stuff yourself.
Our built-in serializers pack data very tightly and use unaligned reads already.
On modern Intel x86 unaligned data access is [as fast or very close](https://lemire.me/blog/2012/05/31/data-alignment-for-speed-myth-or-reality/)
as unaligned one.

One valid use case when aligned data is preferred is navigating a huge
buffer with bitwise pow2 calculations to get an offset without integer
division. For that see the previous question or use [floating-point division](https://lemire.me/blog/2017/11/16/fast-exact-integer-divisions-using-floating-point-operations/).