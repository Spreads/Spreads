// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Buffers;
using Spreads.Collections.Concurrent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spreads.Storage {

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct RawColumnChunk {
        public readonly int PanelId;
        public readonly int ColumnId;
        public readonly long ChunkKey;
        public readonly long LastKey;
        public readonly long Version;
        public readonly int Count;
        public readonly PreservedMemory<byte> Keys;
        public readonly PreservedMemory<byte> Values;

        public RawColumnChunk(int panelId, int columnId, long chunkKey, long lastKey, long version, int count,
            PreservedMemory<byte> keys, PreservedMemory<byte> values) {
            PanelId = panelId;
            ColumnId = columnId;
            ChunkKey = chunkKey;
            LastKey = lastKey;
            Version = version;
            Count = count;
            Keys = keys;
            Values = values;
        }
    }

    public class RawPanelChunk : IDisposable {

        // these objects are frequently used, but are not short-lived and could survive gen0 often
        private static readonly MultipleProducerConsumerQueue Pool = new MultipleProducerConsumerQueue(64);

        private List<RawColumnChunk> _columns;
        private RawColumnChunk _prime;

        private RawPanelChunk() {
            // enforce pool usage by hiding the constructor
        }

        public RawColumnChunk Prime => _prime;
        public int PanelId => _prime.PanelId;
        public long ChunkKey => _prime.ChunkKey;
        public long LastKey => _prime.LastKey;
        public int Count => _prime.Count;
        public int ColumnCount => _columns?.Count ?? 0;

        /// <summary>
        /// Has at least one non-prime column
        /// </summary>
        public bool IsPanel => _columns.Count > 0;

        /// <summary>
        /// True if the prime series is empty
        /// </summary>
        public bool IsPrimeEmpty => _prime.Values.Memory.IsEmpty;

        public void Add(RawColumnChunk columnChunk) {
            // TODO review, here we assume that panelId <> 0 indicates the Prime field was set, could be broken if we refactor something
            if (_prime.PanelId == 0) {
                if (columnChunk.ColumnId != 0 || columnChunk.PanelId == 0) {
                    throw new InvalidOperationException(
                        "The first column chunk must be a prime chunk with the ColumnId equals zero and PanelId <> 0");
                }
                _prime = columnChunk;
            } else {
                // NB lazy for panels, never for simple Prime-only series
                if (_columns == null) _columns = new List<RawColumnChunk>();
                var lastColumnId = _columns[_columns.Count - 1].ColumnId;
                if (columnChunk.PanelId != Prime.PanelId) {
                    throw new InvalidOperationException(
                        "Columns must have the same PanelId");
                }
                if (columnChunk.ColumnId <= lastColumnId) {
                    throw new InvalidOperationException(
                        "Columns must be added in increasing-by-column-id order");
                }
                if (columnChunk.Count > _prime.Count) {
                    throw new InvalidOperationException(
                        "Columns count must be less of equal to their RawPanelChunk Prime count");
                }
                _columns.Add(columnChunk);
            }
        }

        public RawColumnChunk this[int index] => _columns[index];

        public RawColumnChunk GetColumnById(int columnId) {
            if (columnId == 0) {
                return Prime;
            }
            // NB linear serach will win in most cases, typical widths is small, do not bother with BinarySearch even though the _column list is sorted by ColumnId
            foreach (var column in _columns) {
                if (column.ColumnId == columnId) {
                    return column;
                }
            }
            throw new KeyNotFoundException("ColumnId is not present in this RawPanelChunk");
        }

        public void Clear() {
            for (var i = 0; i < _columns.Count; i++) {
                var keys = _columns[i].Keys;
                keys.Dispose();
                var values = _columns[i].Values;
                values.Dispose();
            }
            var pkeys = Prime.Keys;
            pkeys.Dispose();
            var pvalues = Prime.Values;
            pvalues.Dispose();
            _prime = default(RawColumnChunk);
            _columns.Clear();
        }

        public static RawPanelChunk Create() {
            object pooled;
            if (Pool.TryDequeue(out pooled)) {
                var asRawPanelChunk = (RawPanelChunk)pooled;
                Debug.Assert(asRawPanelChunk != null && asRawPanelChunk._columns.Count == 0);
                return asRawPanelChunk;
            }
            return new RawPanelChunk();
        }

        public void Dispose() {
            this.Clear();
            Pool.TryEnqueue(this);
        }
    }
}
