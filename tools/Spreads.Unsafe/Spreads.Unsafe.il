
.assembly extern System.Runtime
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 4:0:0:0
}
.assembly Spreads.Unsafe
{
  .custom instance void [System.Runtime]System.Runtime.CompilerServices.RuntimeCompatibilityAttribute::.ctor()
           = {property bool 'WrapNonExceptionThrows' = bool(true)}
  .custom instance void [System.Runtime]System.Reflection.AssemblyFileVersionAttribute::.ctor(string)
           = {string('1.0.1.0')}
  .custom instance void [System.Runtime]System.Reflection.AssemblyInformationalVersionAttribute::.ctor(string)
           = {string('1.0.1.0')}
  .custom instance void [System.Runtime]System.Reflection.AssemblyTitleAttribute::.ctor(string)
           = {string('Spreads.Unsafe')}
  .custom instance void [System.Runtime]System.Reflection.AssemblyDescriptionAttribute::.ctor(string)
           = {string('Spreads.Unsafe')}


  .custom instance void [System.Runtime]System.CLSCompliantAttribute::.ctor(bool)
           = {bool(false)}
  .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilationRelaxationsAttribute::.ctor(int32)
           = {int32(8)}
  .publickey = (00 24 00 00 04 80 00 00 94 00 00 00 06 02 00 00   // .$..............
                00 24 00 00 52 53 41 31 00 04 00 00 01 00 01 00   // .$..RSA1........
                4B 86 C4 CB 78 54 9B 34 BA B6 1A 3B 18 00 E2 3B   // K...xT.4...;...;
                FE B5 B3 EC 39 00 74 04 15 36 A7 E3 CB D9 7F 5F   // ....9.t..6....._
                04 CF 0F 85 71 55 A8 92 8E AA 29 EB FD 11 CF BB   // ....qU....).....
                AD 3B A7 0E FE A7 BD A3 22 6C 6A 8D 37 0A 4C D3   // .;......"lj.7.L.
                03 F7 14 48 6B 6E BC 22 59 85 A6 38 47 1E 6E F5   // ...Hkn."Y..8G.n.
                71 CC 92 A4 61 3C 00 B8 FA 65 D6 1C CE E0 CB E5   // q...a<...e......
                F3 63 30 C9 A0 1F 41 83 55 9F 1B EF 24 CC 29 17   // .c0...A.U...$.).
                C6 D9 13 E3 A5 41 33 3A 1D 05 D9 BE D2 2B 38 CB ) // .....A3:.....+8.
  .hash algorithm 0x00008004
  .ver 1:0:1:0
}
.module Spreads.Unsafe.dll
// MVID: {1044402A-C66E-4306-A842-AE8CEEB0AAEB}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000009    //  ILONLY
// Image base: 0x02E10000


// =============== CLASS MEMBERS DECLARATION ===================

.class public abstract auto ansi sealed beforefieldinit Spreads.UnsafeEx
       extends [System.Runtime]System.Object
{
  
  .method public hidebysig static int32 CalliCompress(uint8* source, native int sourceLength, uint8* destination, native int destinationLength, int32 clevel, native int functionPtr) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  6
	ldarg.0
    ldarg.1
    ldarg.2
    ldarg.3
	ldarg 4
	ldarg 5
	calli unmanaged cdecl int32 (uint8* source, native int sourceLength, uint8* destination, native int destinationLength, int32 clevel) 
	ret
  }

  .method public hidebysig static int32 CalliDecompress(uint8* source, native int sourceLength, uint8* destination, native int destinationLength, native int functionPtr) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  5
	ldarg.0
    ldarg.1
    ldarg.2
    ldarg.3
	ldarg 4
	calli unmanaged cdecl int32 (uint8* source, native int sourceLength, uint8* destination, native int destinationLength) 
	ret
  }

  .method public hidebysig static int32  CompareToConstrained<T>(!!T& left,
                                                                 !!T& right) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  ldobj      !!T
    IL_0007:  constrained. !!T
    IL_000d:  callvirt   instance int32 class [System.Runtime]System.IComparable`1<!!T>::CompareTo(!0)
    IL_0012:  ret
  } // end of method Unsafe::CompareToConstrained

  .method public hidebysig static bool  EqualsConstrained<T>(!!T& left,
                                                             !!T& right) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  ldobj      !!T
    IL_0007:  constrained. !!T
    IL_000d:  callvirt   instance bool class [System.Runtime]System.IEquatable`1<!!T>::Equals(!0)
    IL_0012:  ret
  } // end of method Unsafe::EqualsConstrained

  .method public hidebysig static int32  GetHashCodeConstrained<T>(!!T& obj) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  constrained. !!T
    IL_0007:  callvirt   instance int32 [System.Runtime]System.Object::GetHashCode()
    IL_000c:  ret
  } // end of method Unsafe::GetHashCodeConstrained

  .method public hidebysig static void  DisposeConstrained<T>(!!T& obj) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  constrained. !!T
    IL_0007:  callvirt   instance void [System.Runtime]System.IDisposable::Dispose()
    IL_000c:  ret
  } // end of method Unsafe::DisposeConstrained

  .method public hidebysig static !!T  AddDeltaConstrained<T>(!!T& obj,
                                                              !!T& delta) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  ldobj      !!T
    IL_0007:  constrained. !!T
    IL_000d:  callvirt   instance !0 class Spreads.IDelta`1<!!T>::AddDelta(!0)
    IL_0012:  ret
  } // end of method Unsafe::AddDeltaConstrained

  .method public hidebysig static !!T  GetDeltaConstrained<T>(!!T& obj,
                                                              !!T& other) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  ldobj      !!T
    IL_0007:  constrained. !!T
    IL_000d:  callvirt   instance !0 class Spreads.IDelta`1<!!T>::GetDelta(!0)
    IL_0012:  ret
  } // end of method Unsafe::GetDeltaConstrained

  .method public hidebysig static !!T  AddLongConstrained<T>(!!T& obj,
                                                             int64 delta) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  constrained. !!T
    IL_0008:  callvirt   instance !0 class Spreads.IInt64Diffable`1<!!T>::Add(int64)
    IL_000d:  ret
  } // end of method Unsafe::AddLongConstrained

  .method public hidebysig static int64  DiffLongConstrained<T>(!!T& left,
                                                                !!T& right) cil managed aggressiveinlining
  {
    .custom instance void System.Runtime.Versioning.NonVersionableAttribute::.ctor()
             = {}
    // 
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  ldarg.1
    IL_0002:  ldobj      !!T
    IL_0007:  constrained. !!T
    IL_000d:  callvirt   instance int64 class Spreads.IInt64Diffable`1<!!T>::Diff(!0)
    IL_0012:  ret
  } // end of method Unsafe::DiffLongConstrained

} // end of class Spreads.Unsafe

.class private auto ansi sealed beforefieldinit System.Runtime.Versioning.NonVersionableAttribute
       extends [System.Runtime]System.Attribute
{
  .custom instance void [System.Runtime]System.AttributeUsageAttribute::.ctor(valuetype [System.Runtime]System.AttributeTargets) = ( 01 00 6C 00 00 00 02 00 54 02 0D 41 6C 6C 6F 77   // ..l.....T..Allow
                                                                                                                                     4D 75 6C 74 69 70 6C 65 00 54 02 09 49 6E 68 65   // Multiple.T..Inhe
                                                                                                                                     72 69 74 65 64 00 )                               // rited.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // 
    .maxstack  1
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [System.Runtime]System.Attribute::.ctor()
    IL_0006:  ret
  } // end of method NonVersionableAttribute::.ctor

} // end of class System.Runtime.Versioning.NonVersionableAttribute

.class interface public abstract auto ansi Spreads.IDelta`1<T>
{
  .method public hidebysig newslot abstract virtual 
          instance !T  AddDelta(!T delta) cil managed
  {
  } // end of method IDelta`1::AddDelta

  .method public hidebysig newslot abstract virtual 
          instance !T  GetDelta(!T other) cil managed
  {
  } // end of method IDelta`1::GetDelta

} // end of class Spreads.IDelta`1

.class interface public abstract auto ansi Spreads.IInt64Diffable`1<T>
       implements class [System.Runtime]System.IComparable`1<!T>
{
  .method public hidebysig newslot abstract virtual 
          instance !T  Add(int64 diff) cil managed
  {
  } // end of method IInt64Diffable`1::Add

  .method public hidebysig newslot abstract virtual 
          instance int64  Diff(!T other) cil managed
  {
  } // end of method IInt64Diffable`1::Diff

} // end of class Spreads.IInt64Diffable`1


// =============================================================

// 
