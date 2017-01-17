// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Spreads.Collections.Concurrent {

    /// <summary>
    /// A debugger view of the IProducerConsumerCollection that makes it simple to browse the
    /// collection's contents at a point in time.
    /// </summary>
    /// <typeparam name="T">The type of elements stored within.</typeparam>
    internal sealed class IProducerConsumerCollectionDebugView<T> {
        private readonly IProducerConsumerCollection<T> _collection; // The collection being viewed.

        /// <summary>
        /// Constructs a new debugger view object for the provided collection object.
        /// </summary>
        /// <param name="collection">A collection to browse in the debugger.</param>
        public IProducerConsumerCollectionDebugView(IProducerConsumerCollection<T> collection) {
            if (collection == null) {
                throw new ArgumentNullException(nameof(collection));
            }

            _collection = collection;
        }

        /// <summary>
        /// Returns a snapshot of the underlying collection's elements.
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get { return _collection.ToArray(); }
        }
    }
}
