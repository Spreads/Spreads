// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;

namespace Spreads
{
    // TODO Common method patterns
    // Empty singletons
    // Create() - new empty containers, logically they should be append/mutable, there is not much of use from empty immutable container
    // CreateFromXXX() - from existing data in memory, immutable
    // CreateAppendXXX() - from existing data, append-only. Always copy data.
    // CreateMutableXXX() - from existing data, mutable. Always copy data.

    // Common parameters
    // transferOwnership - do not copy data but use as is, e.g. arrays. Structural sharing is possible for immutable <-> immutable (including AppendOnly regions to count)

    // Mutability
    // Series could be fully mutable
    // Matrix is just series of T[] so could be fully mutable by rows and individual values
    // Frame could be mutable by rows and individual value. Frame is collection of series with shared keys.
    // Multi-key columns is Frame3D, similar to Sheets in Excel. All columns must share keys for fast math.
    // Panel is lazy collection of series as columns. We store its member content as series of sets of series. Frame is a panel.
    // Panel should be materialized as Frame and then mutated.

    // Optional keys
    // Missing values should be really missing.
    // We could implement sparse VectorStorage with indices+SortedSearch or BitMap. This is a lot of work to get right & fast.
    // Should not **store** data as Opt<>, this kills performance and takes so much space, but it could be used as return value.
    // But Opt<> is just another type, nothing stops users from giving up on vectorized operations and use a lot of space. Only SUM is probably relevant.
    // Panels are the solution for irregular data. If a Frame needs to store missing values then this is an indicator that data should be stored separately.
    // The only valid usage for Opt<> is raw data if a value for a column was actually missing in a row. Null/default and missing mean different things.
    // For Pane columns we probably need sparse chunk-member index that maps to append-only panel column index. TODO review

    /// <summary>
    /// Entry and extension point to Spreads.
    /// </summary>
    public static class Data
    {
        static Data()
        {
            // init this so that further usage is treated as constant
            if (!AdditionalCorrectnessChecks.Enabled)
            {
#if !DEBUG
                Trace.TraceWarning("Additional correctness checks are DISABLED. \n" +
                                       "It's better to keep them ON in production code unless performance is maxed out \n" +
                                       "and production code worked correctly long enough with the additional checks.");
#endif
            }
        }

        // We could define extension method on these objects in other parts
        // and import them via "using static Spreads.Data".

        public static readonly StreamData Stream = default;
        public static readonly SeriesData Series = default;
        public static readonly MatrixData Matrix = default;
        public static readonly FrameData Frame = default;
        public static readonly PanelData Panel = default;
        internal static readonly QueryData Q = default;

        // Reverse naming

        public readonly struct StreamData
        {
            // TODO we do not have a DataSpteam container, only interface - it's just series
            // We need implementation for accumulator/subject with guaranteed capacity
            // We already have persistent ones that require serialization

            public object CreateStream()
            {
                return null;
            }
        }

        public readonly struct SeriesData
        {
            public object Create<TKey, TValue>(int capacity)
            {
                throw new NotImplementedException();
            }

            public object CreateFromArrays<TKey, TValue>(TKey[] keys, TValue[] values, int start = -1, int length = -1, bool transferOwnership = false)
            {
                throw new NotImplementedException();
            }
        }

        public readonly struct MatrixData
        { }

        public readonly struct FrameData
        { }

        public readonly struct PanelData
        { }

        public readonly struct QueryData
        {
            internal static IQueryHandler QueryHandler;

            public DataObject this[string query]
            {
                get
                {
                    if (QueryHandler != null)
                    {
                        return QueryHandler.Query(query);
                    }
                    throw new NotImplementedException("Query handler is not implemented.");
                }
            }
        }

        internal interface IQueryHandler
        {
            DataObject Query(string query);
        }
    }

    // TODO dynamic operations + interface
    public readonly struct DataObject
    {
        private void Test()
        {
            var x = Data.Q["asd"];
            // Data.Series.CreateFrom()
        }
    }
}
