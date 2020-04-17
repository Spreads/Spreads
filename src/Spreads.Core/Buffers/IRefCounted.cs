// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;

namespace Spreads.Buffers
{
    public interface IRefCounted : IDisposable
    {
        int ReferenceCount { get; }

        int Increment();

        int Decrement();

        bool IsDisposed { get; }
    }
}