# Docs sandbox

Unstructured snippets of documentation drafts that need to bew reviewed, polished and moved to proper sections of documentation.



## DataTypeHeader

### TypeEnumOrFixedSize (TEOFS)

* 0 - None/Unit type, meaningful absense of value (vs. null/defalt value of some type).
* 63 known fixed-sized types with 16 byte size limit.
* 63 known var-sized types, inclusing known containers.

Highest bit zero indicates a known type.

Unknown fized-sized types have the highest bit set. Next 7 bits represent the fixed size 
of an unknown type. Values with fixed size > 127 are treated as var-sized. These should be 
extremely rare. If a wide row of fixed-size value is needed we have frame + schema (TODO).

```
Known types
0 1 2 3 4 5 6 7 8
+-+-+-+-+-+-+-+-+
|0|  TypeEnum   |
+---------------+

Unknown types
0 1 2 3 4 5 6 7 8
+-+-+-+-+-+-+-+-+
|1|    Size     |
+---------------+

 U - unknown fixed-sized type.
 V - var-sized type or container.
 TyEn - TypeEnum when U is not set
 USize - 

```

Var-sized type have length prefix LEB128 encoded. 