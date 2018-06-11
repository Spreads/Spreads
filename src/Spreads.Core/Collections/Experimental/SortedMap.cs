using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Spreads.Collections.Experimental
{

    // Rewrite of initial (and battle-tested) F# version using
    // Memory<T> instead of arrays as the backing

    public class SortedMap<K,V>
    {
        private SyncronizationState _sync;

        private bool _isKeyValueLayout = false;





        internal readonly struct SyncronizationState
        {
            private readonly Memory<long> _memory;

            public const int Length = 4;

            public SyncronizationState(Memory<long> memory)
            {
                if (memory.Length != Length)
                {
                    ThrowHelper.ThrowArgumentException(nameof(memory));
                }
                _memory = memory;
            }


        }
    }
}
