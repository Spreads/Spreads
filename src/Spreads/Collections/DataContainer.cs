// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Utils;

namespace Spreads.Collections
{
    /// <summary>
    /// Owns a reference in <see cref="DataBlock"/>, synchronizes access and manages subscriptions.
    /// </summary>
    [CannotApplyEqualityOperator]
    internal sealed partial class DataContainer : IDataSource, IAsyncCompleter, IDisposable
    {
        // TODO review, for series over borrowed blocks
        public static readonly DataContainer NullSentinel = new DataContainer();

        internal DataBlock? Data;
        internal DataBlock? LastBlock;

        internal Flags Flags;

        private int _locker;

        // See http://joeduffyblog.com/2009/06/04/a-scalable-readerwriter-scheme-with-optimistic-retry/
        internal volatile int OrderVersion;
        internal volatile int NextOrderVersion;

        internal volatile int Version;

        public ContainerLayout ContainerLayout => Flags.ContainerLayout;

        public Mutability Mutability => Flags.Mutability;

        public KeySorting KeySorting => Flags.KeySorting;

        public bool IsCompleted => Mutability == Mutability.ReadOnly;

        public ulong? RowCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Data.Height == 0)
                {
                    return (ulong) Data.RowCount;
                }

                return RowCountImpl();
            }
        }

        protected ulong? RowCountImpl()
        {
            return null;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Data.RowCount == 0;
        }

        private void Dispose(bool disposing)
        {
            var data = Interlocked.Exchange(ref Data, null);
            if (data == null)
                ThrowHelper.ThrowObjectDisposedException("DataContainer");
            data.Decrement();
        }

        internal bool IsDisposed => Data == null;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DataContainer()
        {
            Trace.TraceWarning("Finalizing DataContainer. This should not normally happen and containers should be explicitly disposed.");
            Dispose(false);
        }
    }
}