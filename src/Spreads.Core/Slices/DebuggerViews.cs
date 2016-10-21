// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.


// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Spreads.Slices
{
    internal class SpanDebuggerView<T>
    {
        private ReadOnlySpan<T> _slice;

        public SpanDebuggerView(ReadOnlySpan<T> slice)
        {
            _slice = slice;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                return _slice.CreateArray();
            }
        }
    }
}
