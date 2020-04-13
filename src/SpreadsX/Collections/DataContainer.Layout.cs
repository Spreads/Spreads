// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Spreads.Collections.Internal;

namespace Spreads.Collections
{
    /// <summary>
    /// Contains data root, which is DataBlock or DataBlockSource.
    /// </summary>
    public partial class DataContainer
    {
        protected DataContainer()
        {
        }

        /// <summary>
        /// Container-specific data. Never null - instead <see cref="DataBlock.Empty"/> is used as a sentinel.
        /// </summary>
        [NotNull]
        internal object? Data = DataBlock.Empty;

        [NotNull]
        internal DataBlock? DataRoot = DataBlock.Empty;
        
        private long _padding0;
        private long _padding1;
        private long _padding2;
        private long _padding3;
        private long _padding4;
        private long _padding5;
        private long _padding6;
        private long _padding7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDataBlock<TKey>(object data, [NotNullWhen(returnValue: true)] out DataBlock? dataBlock,
            [NotNullWhen(returnValue: false)] out DataBlockSource<TKey>? dataSource)
        {
            var d = data;
            // They are mutually exclusive so save one isinst call,
            // and we could later replace isinst with bool field (there is padding space so it's free, need to benchmark)
            if (d is DataBlock db)
            {
                dataBlock = db;
                dataSource = null;
                return true;
            }

            dataBlock = null;
            dataSource = Unsafe.As<DataBlockSource<TKey>>(d);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDataLeaf<TKey>(DataBlock dataRoot, [NotNullWhen(returnValue: true)] out DataBlock? dataBlock,
            [NotNullWhen(returnValue: false)] out DataBlockSource2<TKey> dataSource)
        {
            if (dataRoot.Height == 0)
            {
                dataBlock = dataRoot;
                dataSource = default;
                return true;
            }

            dataBlock = null;
            dataSource = new DataBlockSource2<TKey>(dataRoot);
            return false;
        }
    }
}