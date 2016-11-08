// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


using System;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices {

    /// <summary>
    /// Contains generic, low-level functionality for manipulating pointers.
    /// </summary>
    public static class UnsafeEx {
        /// <summary>
        /// Reads a value of type T from the given location.
        /// </summary>
        /// <typeparam name="T">The type to read.</typeparam>
        /// <param name="source">The location to read from.</param>
        /// <returns>An object of type T read from the given location.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILSub(@"
            .maxstack 1
            ldarg.0
            unaligned. 1 
            ldobj !!T
            ret")]
        public unsafe static T ReadUnaligned<T>(void* source) {
            throw new NotImplementedException();
        }


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 1
        //    ldarg.0
        //    ldobj !!T
        //    ret")]
        //public unsafe static T ReadAligned<T>(void* source) {
        //    throw new NotImplementedException();
        //}


        /// <summary>
        /// Writes a value of type T to the given location.
        /// </summary>
        /// <typeparam name="T">The type of value to write.</typeparam>
        /// <param name="destination">The location to write to.</param>
        /// <param name="value">The value to write.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ILSub(@"
            .maxstack 2
            ldarg.0
            ldarg.1
            unaligned. 1 
            stobj !!T
            ret")]
        public unsafe static void WriteUnaligned<T>(void* destination, T value) {
            throw new NotImplementedException();
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 2
        //    ldarg.0
        //    ldarg.1
        //    stobj !!T
        //    ret")]
        //public unsafe static void WriteAligned<T>(void* destination, T value) {
        //    throw new NotImplementedException();
        //}


        /// <summary>
        /// Copies a value of type T to the given location.
        /// </summary>
        /// <typeparam name="T">The type of value to copy.</typeparam>
        /// <param name="destination">The location to copy to.</param>
        /// <param name="source">A reference to the value to copy.</param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 2
        //    ldarg.0
        //    ldarg.1
        //    ldobj !!T
        //    unaligned. 1 
        //    stobj !!T
        //    ret")]
        //public unsafe static void Copy<T>(void* destination, ref T source) {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Copies a value of type T to the given location.
        /// </summary>
        /// <typeparam name="T">The type of value to copy.</typeparam>
        /// <param name="destination">The location to copy to.</param>
        /// <param name="source">A pointer to the value to copy.</param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 2
        //    ldarg.0
        //    ldarg.1
        //    unaligned. 1 
        //    ldobj !!T
        //    stobj !!T
        //    ret")]
        //public unsafe static void Copy<T>(ref T destination, void* source) {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Returns a pointer to the given by-ref parameter.
        /// </summary>
        /// <typeparam name="T">The type of object.</typeparam>
        /// <param name="value">The object whose pointer is obtained.</param>
        /// <returns>A pointer to the given value.</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 1
        //    ldarg.0
        //    conv.u
        //    ret")]
        //public unsafe static void* AsPointer<T>(ref T value) {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Returns the size of an object of the given type parameter.
        /// </summary>
        /// <typeparam name="T">The type of object whose size is retrieved.</typeparam>
        /// <returns>The size of an object of type T.</returns>

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    sizeof !!T
        //    ret")]
        //public static int SizeOf<T>() {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Copies bytes from the source address to the destination address.
        /// </summary>
        /// <param name="destination">The destination address to copy to.</param>
        /// <param name="source">The source address to copy from.</param>
        /// <param name="byteCount">The number of bytes to copy.</param>

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 3
        //    ldarg.0
        //    ldarg.1
        //    ldarg.2
        //    unaligned. 1 
        //    cpblk
        //    ret")]
        //public unsafe static void CopyBlock(void* destination, void* source, uint byteCount) {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Initializes a block of memory at the given location with a given initial value.
        /// </summary>
        /// <param name="startAddress">The address of the start of the memory block to initialize.</param>
        /// <param name="value">The value to initialize the block to.</param>
        /// <param name="byteCount">The number of bytes to initialize.</param>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 3
        //    ldarg.0
        //    ldarg.1
        //    ldarg.2
        //    unaligned. 1 
        //    initblk
        //    ret")]
        //public unsafe static void InitBlock(void* startAddress, byte value, uint byteCount) {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Casts the given object to the specified type.
        /// </summary>
        /// <typeparam name="T">The type which the object will be cast to.</typeparam>
        /// <param name="o">The object to cast.</param>
        /// <returns>The original object, casted to the given type.</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 1
        //    ldarg.0
        //    ret")]
        //public static T As<T>(object o) where T : class {
        //    throw new NotImplementedException();
        //}

        /// <summary>
        /// Reinterprets the given location as a reference to a value of type T.
        /// </summary>
        /// <typeparam name="T">The type of the interpreted location.</typeparam>
        /// <param name="source">The location of the value to reference.</param>
        /// <returns>A reference to a value of type T.</returns>
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 1
        //    ldarg.0
        //    ret")]
        //public unsafe static T AsRef<T>(void* source) {
        //    throw new NotImplementedException();
        //}

        // NB no way to call private ctor
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[ILSub(@"
        //    .maxstack 1
        //    ldarg.0
        //    newobj instance void valuetype [System.Numerics.Vectors]System.Numerics.Vector`1<!!T>::.ctor(void*)
        //    ret")]
        //public unsafe static Vector<T> AsVector<T>(void* source) where T : struct
        //{
        //    throw new NotImplementedException();
        //}

    }
}
