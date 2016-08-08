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
using Spreads.Serialization;

namespace Spreads.Storage {

    public interface IPersistentMap<TKey, TValue> : IDictionary<TKey, TValue>, IPersistentObject
    {
        // TODO methods
        // TryAdd
        // AddOrUpdate
        // and others similar to ConcurrentDictionary

        TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory);
    }

    //[DebuggerTypeProxy(typeof(IDictionaryDebugView<,>))]
    //[DebuggerDisplay("Count = {Count}")]
    //[System.Runtime.InteropServices.ComVisible(false)]
    public sealed class PersistentMap<TKey, TValue> {
        public static IPersistentMap<TKey, TValue> Open(string fileName, int capacity = 5) {
            if (BinarySerializer.Size<PersistentMapFixedLength<TKey, TValue>.Entry>() > 0 
                || (BinarySerializer.Size<TKey>() > 0 && BinarySerializer.Size<TValue>() > 0)) {
                return new PersistentMapFixedLength<TKey, TValue>(fileName, capacity);
            }
            throw new NotImplementedException();
        }
    }
}