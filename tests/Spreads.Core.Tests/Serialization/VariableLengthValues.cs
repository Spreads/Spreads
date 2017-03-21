// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using NUnit.Framework;
using Spreads.Collections;
using Spreads.DataTypes;
using System;

namespace Spreads.Core.Tests.Serialization
{
    [TestFixture]
    public class VariableLengthValuesTests
    {
        [Test, Ignore]
        public void CouldCreateSortemMapWithMemoryTAsValue()
        {
            var sm = new SortedMap<DateTime, Memory<Tick>>();
            var rng = new System.Random();
            for (int i = 0; i < 1000; i++)
            {
                var valueCount = rng.Next(1, 10);
                for (int j = 0; j < valueCount; j++)
                {
                    // TODO issue 93 could be implemented with Memory<T> or PreservedBuffer<T>
                    // * Not clear how to dispose buffers when SM is disposed (e.g. should array pools
                    //   do this for IDisposable types in the Clear() method?)
                    // * Not clear how addition should happen: Memory is fixed-length, but we do not know the number
                    //   of value elements per key. If we allocate a big OwnedMemory chunk and append to it until a key
                    //   changes then that should be done in some container that manages the process
                    // * Could end up with a new collection `DataStream<K,V> : ISeries<K,IEnumerable<V>>`, in which values
                    //   are indexed by key but could be duplicate. Or a wrapper around SCM that provides a hasher function
                    //   which switches to a new SM backed by a new OwnedMemory, blocks non-apppend writes and flushes
                    //   current multi-value on dispossing.
                }
            }
        }
    }
}