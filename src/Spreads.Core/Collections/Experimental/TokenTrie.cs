using System;
using System.Collections;
using System.Collections.Generic;

namespace Spreads.Collections.Experimental
{
    // ConcurrentDictionary enumerator allocates, that defeats the purpose of such struct
    // Also this is single-process. Could prototype here, but implementation should be
    // persistent x-process (which means LMDB)

    internal class TokenTrie<TToken, TValue> where TToken : IEquatable<TToken>
    {
        internal struct Node
        {
            // public TToken Token;
            public TValue Value;

            public Dictionary<TToken, Node> Leafs;
        }

        private Node _root = new Node() { Leafs = new Dictionary<TToken, Node>() };

        public struct ValueGroup : IEnumerable<TValue>
        {
            public IEnumerator<TValue> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public ValueGroup this[TToken[] index] // not array but struct enumerable
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }
}
