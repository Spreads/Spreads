/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Storage {
    // ReSharper disable once EnumUnderlyingTypeIsInt
    public enum MessageType : int { // TODO why int and not byte? 
        Set = 0,
        Complete = 10,
        Remove = 20,
        Append = 30,
        SetChunk = 40,
        /// <summary>
        /// Message buffer has serialized chunk
        /// </summary>
        ChunkBody = 41,
        RemoveChunk = 50,
        Subscribe = 60,
        Synced = 70,
        WriteRequest = 80,
        /// <summary>
        /// Has Pid of the current writer for series. Used as a response to WriteRequest.
        /// </summary>
        CurrentWriter = 81,

        WriteRelease = 90,


        Error = -1,

        /// <summary>
        /// Generic command that depends on context
        /// </summary>
        Broadcast = int.MaxValue,

        /// <summary>
        /// Opaque conductor message
        /// </summary>
        ConductorMessage = int.MinValue

    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 28)]
    internal struct MessageHeader {
        public UUID UUID;
        public MessageType MessageType;
        public long Version;

        public static int Size => 28;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [Serialization(PreferBlittable = true)]
    internal struct SetRemoveCommandBody<TKey, TValue> : IBinaryConverter<SetRemoveCommandBody<TKey, TValue>> {
        public TKey key; // Key of entry
        public TValue value; // Value of entry
        private static readonly bool _isFixedSize = TypeHelper<TKey>.Size > 0 && TypeHelper<TValue>.Size > 0;
        private static readonly int _size = _isFixedSize ? 8 + TypeHelper<TKey>.Size + TypeHelper<TValue>.Size : -1;
        // NB this interface methods are only called when SetRemoveCommandBody is not directly pinnable
        // Otherwise more efficient direct conversion is used
        public bool IsFixedSize => TypeHelper<TKey>.Size > 0 && TypeHelper<TValue>.Size > 0;
        public int Size => IsFixedSize ? 8 + TypeHelper<TKey>.Size + TypeHelper<TValue>.Size : -1;
        public int SizeOf(SetRemoveCommandBody<TKey, TValue> bodyValue, out MemoryStream payloadStream) {
            if (IsFixedSize) {
                payloadStream = null;
                return Size;
            }

            payloadStream = RecyclableMemoryManager.MemoryStreams.GetStream("SetRemoveCommandBody.SizeOf");
            Debug.Assert(payloadStream.Position == 0);

            payloadStream.WriteAsPtr<int>(Version);

            // placeholder for length
            payloadStream.WriteAsPtr<int>(0);
            Debug.Assert(payloadStream.Position == 8);

            if (TypeHelper<TKey>.Size <= 0) {
                throw new NotImplementedException("TODO We now only support fixed key");
            }

            var size = 8 + TypeHelper<TKey>.Size;
            payloadStream.WriteAsPtr<TKey>(bodyValue.key);
            MemoryStream valueMs;
            var valueSize = TypeHelper<TValue>.SizeOf(bodyValue.value, out valueMs);
            if (valueMs != null) {
                valueMs.WriteTo(payloadStream);
                valueMs.Dispose();
            }

            size += valueSize;

            payloadStream.Position = 4;
            payloadStream.WriteAsPtr<int>((int)payloadStream.Length - 8);
            Trace.Assert(size == payloadStream.Length);
            payloadStream.Position = 0;
            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToPtr(SetRemoveCommandBody<TKey, TValue> entry, IntPtr ptr, MemoryStream payloadStream = null) {
            if (IsFixedSize) {
                TypeHelper<TKey>.ToPtr(entry.key, (ptr));
                TypeHelper<TValue>.ToPtr(entry.value, (ptr + TypeHelper<TKey>.Size));
                return Size;
            } else {
                if (payloadStream == null) {
                    MemoryStream tempStream;
                    // here we know that is not IsFixedSize, SizeOf will return MS
                    var size = SizeOf(entry, out tempStream);
                    Debug.Assert(tempStream != null && size == tempStream.Length);
                    tempStream.WriteToPtr(ptr);
                    tempStream.Dispose();
                    return size;
                } else {
                    payloadStream.WriteToPtr(ptr);
                    // do not dispose, MS is owned outside of this method
                    return checked((int)payloadStream.Length);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once RedundantAssignment
        public int FromPtr(IntPtr ptr, ref SetRemoveCommandBody<TKey, TValue> body) {
            if (IsFixedSize) {
                var entry = new SetRemoveCommandBody<TKey, TValue>();
                var kl = TypeHelper<TKey>.FromPtr((ptr), ref entry.key);
                var vl = TypeHelper<TValue>.FromPtr((ptr + TypeHelper<TKey>.Size), ref entry.value);
                Debug.Assert(_size == 8 + kl + vl);
                body = entry;
                return _size;
            }
            {
                var version = Marshal.ReadInt32(ptr);
                Debug.Assert(version == 0);
                var length = Marshal.ReadInt32(ptr + 4);
                ptr = ptr + 8;
                var entry = new SetRemoveCommandBody<TKey, TValue>();
                var kl = TypeHelper<TKey>.FromPtr(ptr, ref entry.key);
                var vl = TypeHelper<TValue>.FromPtr((ptr + kl), ref entry.value);
                Debug.Assert(length == kl + vl);
                body = entry;
                return length + 8;
            }
        }

        public byte Version => 0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 20)]
    public struct ChunkCommandBody {
        public long ChunkKey;
        public long Count;
        public int Lookup;
        public static int Size => 20;
    }
}
