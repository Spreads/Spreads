using System;
using System.Collections.Generic;
using System.Diagnostics;
using Spreads.Serialization;

namespace Spreads.Collections.Persistent {

    public interface IPersistentMap<TKey, TValue> : IDictionary<TKey, TValue>, IPersistentObject {
    }

    //[DebuggerTypeProxy(typeof(IDictionaryDebugView<,>))]
    //[DebuggerDisplay("Count = {Count}")]
    //[System.Runtime.InteropServices.ComVisible(false)]
    public sealed class PersistentMap<TKey, TValue> {
        public static IPersistentMap<TKey, TValue> Open(string fileName, int capacity = 5) {
            if (TypeHelper<PersistentMapFixedLength<TKey, TValue>.Entry>.Size > 0 
                || (TypeHelper<TKey>.Size > 0 && TypeHelper<TValue>.Size > 0)) {
                return new PersistentMapFixedLength<TKey, TValue>(fileName, capacity);
            }
            throw new NotImplementedException();
        }
    }
}