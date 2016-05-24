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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage {
    public enum CommandType : int {
        Set = 0,
        Complete = 10,
        Remove = 20,
        Append = 30,
        SetChunk = 40,
        RemoveChunk = 50,
        Subscribe = 60,
        /// <summary>
        /// An active writer MUST flush series in response to subscribe command.
        /// </summary>
        Flush = 70,
        AcquireLock = 80,
        ReleaseLock = 90,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 28)]
    internal struct CommandHeader {
        public UUID SeriesId;
        public CommandType CommandType;
        public long Version;

        public static int Size => 28;
    }

    

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct SetRemoveCommandBody<TKey, TValue> : IBinaryConverter<SetRemoveCommandBody<TKey, TValue>> {
        public TKey key; // Key of entry
        public TValue value; // Value of entry

        // NB this interface methods are only called when SetRemoveCommandBody is not directly pinnable
        // Otherwise more efficient direct conversion is used
        public bool IsFixedSize => TypeHelper<TKey>.Size > 0 && TypeHelper<TValue>.Size > 0;
        public int Size => IsFixedSize ? 8 + TypeHelper<TKey>.Size + TypeHelper<TValue>.Size : -1;
        public int SizeOf(SetRemoveCommandBody<TKey, TValue> value) {
            if (IsFixedSize) return Size;
            throw new NotImplementedException("TODO variable sized types");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToPtr(SetRemoveCommandBody<TKey, TValue> entry, IntPtr ptr) {
            if (IsFixedSize) {
                TypeHelper<TKey>.StructureToPtr(entry.key, (ptr + 8));
                TypeHelper<TValue>.StructureToPtr(entry.value, (ptr + 8 + TypeHelper<TKey>.Size));
            } else {
                throw new NotImplementedException("TODO variable sized types");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SetRemoveCommandBody<TKey, TValue> FromPtr(IntPtr ptr) {
            if (!IsFixedSize) throw new NotImplementedException("TODO variable sized types");
            var entry = new SetRemoveCommandBody<TKey, TValue>();
            entry.key = TypeHelper<TKey>.PtrToStructure((ptr + 8));
            entry.value = TypeHelper<TValue>.PtrToStructure((ptr + 8 + TypeHelper<TKey>.Size));
            return entry;
        }

        public int Version => -1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 20)]
    public struct ChunkCommandBody {
        public long ChunkKey;
        public long Count;
        public int Lookup;
        public static int Size => 20;
    }
}
