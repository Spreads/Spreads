/*
    Copyright(c) 2014-2016 Victor Baybekov.

    This file is a part of Spreads library.

    Spreads library is free software; you can redistribute it and/or modify it under
    the terms of the GNU General Public License as published by
    the Free Software Foundation; either version 3 of the License, or
    (at your option) any later version.

    Spreads library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see<http://www.gnu.org/licenses/>.
*/



using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Collections;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Logbuffer;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage {
    /// <summary>
    /// Persistent series.
    /// </summary>
    public sealed class PersistentSeries<K, V> : Series<K, V>, IPersistentOrderedMap<K, V>, SeriesRepository.IAcceptCommand {
        private readonly IAppendLog _appendLog;
        private readonly int _pid;
        private readonly UUID _uuid;
        private readonly IPersistentOrderedMap<K, V> _innerMap;
        private readonly bool _allowBatches;
        private readonly Action<bool, bool> _disposeCallback;
        internal readonly AsyncAutoResetEvent FlushEvent = new AsyncAutoResetEvent();
        internal readonly AsyncAutoResetEvent LockReleaseEvent = new AsyncAutoResetEvent();
        /// <summary>
        /// Number of time a series was accessed via repository
        /// </summary>
        internal int RefCounter = 0;
        private volatile bool _isWriter;

        internal PersistentSeries(IAppendLog appendLog, int pid, UUID uuid, IPersistentOrderedMap<K, V> innerMap, bool allowBatches, bool isWriter,
            Action<bool, bool> disposeCallback = null) {
            _appendLog = appendLog;
            _pid = pid;
            _uuid = uuid;
            _innerMap = innerMap;
            _allowBatches = allowBatches;
            _isWriter = isWriter;
            var outer = (_innerMap as SortedChunkedMap<K, V>)?.OuterMap as RemoteChunksSeries<K, V>;
            if (outer != null) outer.ReadOnly = !_isWriter;
            _disposeCallback = disposeCallback;
            Interlocked.Increment(ref RefCounter);
            if (TypeHelper<SetRemoveCommandBody<K, V>>.Size == -1) {
                throw new NotImplementedException("TODO variable size support");
            }
        }



        // NB we apply commands only to instantiated series when runtime does all types magic
        public unsafe void ApplyCommand(DirectBuffer buffer) {
            var dataStart = buffer.Data;
            var header = *(CommandHeader*)(dataStart);
            Trace.Assert(header.SeriesId == _uuid);
            var type = header.CommandType;
            switch (type) {
                case CommandType.Set:
                    // If this is a writer, it should ignore its own commands read from log-buffer
                    //if (!_isWriter) throw new ApplicationException("TODO temp delete this");
                    if (!_isWriter) {
                        Trace.Assert(header.Version == _innerMap.Version + 1);
                        var setBody =
                            TypeHelper<SetRemoveCommandBody<K, V>>.PtrToStructure(dataStart + CommandHeader.Size);
                        _innerMap[setBody.key] = setBody.value;
                    }
                    break;

                case CommandType.Remove:
                    if (!_isWriter) {
                        Trace.Assert(header.Version == _innerMap.Version + 1);
                        var removeBody =
                            TypeHelper<SetRemoveCommandBody<K, int>>.PtrToStructure(dataStart + CommandHeader.Size);
                        _innerMap.RemoveMany(removeBody.key, (Lookup)removeBody.value);
                    }
                    break;

                case CommandType.Append:
                    if (!_isWriter) {
                        throw new NotImplementedException("TODO");
                        Trace.Assert(header.Version == _innerMap.Version + 1);
                    }
                    break;

                case CommandType.Complete:
                    if (!_isWriter) {
                        Trace.Assert(header.Version == _innerMap.Version + 1);
                        _innerMap.Complete();
                    }
                    break;

                case CommandType.SetChunk:
                    //if (!_isWriter) {
                    //    var setChunkBody = *(ChunkCommandBody*)(pointer + 4 + CommandHeader.Size);
                    //    {
                    //        var scm = _innerMap as SortedChunkedMap<K, V>;
                    //        var comparer = scm.Comparer as IKeyComparer<K>;
                    //        if (scm != null && comparer != null) {
                    //            var k = comparer.Add(default(K), setChunkBody.ChunkKey);
                    //            // NB by protocol, this event must only happen after a chunk is on disk
                    //            // we should touch it and ensure it is what we think it is
                    //            SortedMap<K, V> sm;
                    //            if (scm.OuterMap.TryGetValue(k, out sm)) {
                    //                if (setChunkBody.Count != sm.Count || header.Version != scm.Version)
                    //                {
                    //                    Trace.TraceWarning("TODO Chunks and mutations could go in different sequence now.");
                    //                    //throw new ApplicationException(
                    //                    //    $"setChunkBody.Count {setChunkBody.Count} != sm.Count {sm.Count} || header.Version {header.Version} != scm.Version {scm.Version}");
                    //                }
                    //            }
                    //        }
                    //    }
                    //}

                    break;

                case CommandType.RemoveChunk:
                    //if (!_isWriter) {
                    //    var setChunkBody = *(ChunkCommandBody*)(pointer + 4 + CommandHeader.Size);
                    //    {
                    //        var scm = _innerMap as SortedChunkedMap<K, V>;
                    //        var comparer = scm.Comparer as IKeyComparer<K>;
                    //        if (scm != null && comparer != null) {
                    //            var k = comparer.Add(default(K), setChunkBody.ChunkKey);
                    //            // NB RemoveMany set prevBucket to null
                    //            scm.OuterMap.RemoveMany(k, (Lookup)setChunkBody.Lookup);
                    //            scm.Flush();
                    //        }
                    //    }
                    //}
                    break;

                case CommandType.Flush:
                    if (!_isWriter) {
                        Trace.Assert(header.Version == _innerMap.Version);
                        FlushEvent.Set();
                    }
                    break;

                case CommandType.Subscribe:
                    // if we are the single writer, we must flush so that new subscribers could see unsaved data
                    // if we are read-only
                    if (_isWriter) this.Flush();
                    break;

                case CommandType.AcquireLock:
                    break;
                case CommandType.ReleaseLock:
                    LockReleaseEvent.Set();
                    // ignore
                    break;

                default:
                    throw new ApplicationException("Explicitly ignore all irrelevant cases here");
            }
            //if (_innerMap.Version != header.Version && !_isWriter) {
            //    Trace.TraceWarning($"_innerMap.Version {_innerMap.Version} != Command header {header.CommandType} version {header.Version}");
            //}
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void LogSetKeyValue(K key, V value) {
            if (_allowBatches) return;
            var header = new CommandHeader {
                SeriesId = _uuid,
                CommandType = CommandType.Set,
                Version = this.Version
            };
            var commandBody = new SetRemoveCommandBody<K, V> {
                key = key,
                value = value
            };
            var len = CommandHeader.Size + TypeHelper<SetRemoveCommandBody<K, V>>.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            TypeHelper<SetRemoveCommandBody<K, V>>.StructureToPtr(commandBody, claim.Data + CommandHeader.Size);
            claim.ReservedValue = _pid;
            claim.Commit();
        }

        public new V this[K key]
        {
            get { return _innerMap[key]; }
            set
            {
                _innerMap[key] = value;
                LogSetKeyValue(key, value);
            }
        }

        public void Add(K key, V value) {
            _innerMap.Add(key, value);
            LogSetKeyValue(key, value);
        }

        public void AddFirst(K key, V value) {
            _innerMap.AddFirst(key, value);
            LogSetKeyValue(key, value);
        }

        public void AddLast(K key, V value) {
            _innerMap.AddLast(key, value);
            LogSetKeyValue(key, value);
        }

        public int Append(IReadOnlyOrderedMap<K, V> appendMap, AppendOption option) {
            throw new NotImplementedException("TODO");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void LogRemoveKeyDirection(K key, int lookup) {
            if (_allowBatches) return;
            var header = new CommandHeader {
                SeriesId = _uuid,
                CommandType = CommandType.Remove,
                Version = this.Version
            };
            var commandBody = new SetRemoveCommandBody<K, int> {
                key = key,
                value = lookup
            };
            var len = CommandHeader.Size + TypeHelper<SetRemoveCommandBody<K, int>>.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            TypeHelper<SetRemoveCommandBody<K, int>>.StructureToPtr(commandBody, claim.Data + CommandHeader.Size);
            claim.ReservedValue = _pid;
            claim.Commit();
        }

        public bool Remove(K key) {

            var res = _innerMap.Remove(key);
            if (res) {
                var lookup = (int)Lookup.EQ;
                LogRemoveKeyDirection(key, lookup);
            }
            return res;
        }

        public bool RemoveFirst(out KeyValuePair<K, V> value) {
            var res = _innerMap.RemoveFirst(out value);
            if (res) {
                var lookup = (int)Lookup.EQ;
                LogRemoveKeyDirection(value.Key, lookup);
            }
            return res;
        }

        public bool RemoveLast(out KeyValuePair<K, V> value) {
            var res = _innerMap.RemoveLast(out value);
            if (res) {
                var lookup = (int)Lookup.EQ;
                LogRemoveKeyDirection(value.Key, lookup);
            }
            return res;
        }

        public bool RemoveMany(K key, Lookup direction) {
            var res = _innerMap.RemoveMany(key, direction);
            if (res) {
                var lookup = (int)direction;
                LogRemoveKeyDirection(key, lookup);
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void LogHeaderOnly(CommandType type) {
            var header = new CommandHeader {
                SeriesId = _uuid,
                CommandType = type,
                Version = this.Version
            };
            var len = CommandHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            claim.ReservedValue = _pid;
            claim.Commit();
        }

        public void Complete() {
            if (_isWriter) {
                _innerMap.Complete();
                LogHeaderOnly(CommandType.Complete);
            } else {
                throw new InvalidOperationException("Cannot Complete read-only series");
            }
        }

        public void Flush() {
            if (_isWriter) {
                _innerMap.Flush();
                LogHeaderOnly(CommandType.Flush);
            } else {
                throw new InvalidOperationException("Cannot Flush read-only series");
            }
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                var cnt = Interlocked.Decrement(ref RefCounter);
                if (cnt < 0) throw new InvalidOperationException("A PersistentSeries was disposed more times than accessed via a repository.");
                if (cnt == 0) {
                    // true means the series will be removed from _openSeries disctionary 
                    _disposeCallback?.Invoke(true, true);
                    _innerMap.Dispose();
                    GC.SuppressFinalize(this);
                } else {
                    // disposing a single writer
                    var shouldDowngrade = _isWriter;
                    if (_isWriter) {
                        Flush();
                        _isWriter = false;
                        var outer = (_innerMap as SortedChunkedMap<K, V>)?.OuterMap as RemoteChunksSeries<K, V>;
                        if (outer != null) outer.ReadOnly = !_isWriter;
                        LockReleaseEvent.Set();
                    }
                    _disposeCallback?.Invoke(false, shouldDowngrade);
                }
                //GC.SuppressFinalize(this);
            } else {
                _innerMap.Dispose();
            }
            // GCed series are just cleared away
        }

        public void Dispose() {
            Dispose(true);
        }

        // NB a series is kept in a dictionary after it was opened
        ~PersistentSeries() {
            Dispose(false);
        }

        //////////////////////////////////// READ METHODS REDIRECT  ////////////////////////////////////////////

        V IReadOnlyOrderedMap<K, V>.this[K key] => _innerMap[key];

        public new IComparer<K> Comparer => _innerMap.Comparer;

        public long Version
        {
            get { return _innerMap.Version; }
            set { _innerMap.Version = value; }
        }

        public long Count => _innerMap.Count;

        public new KeyValuePair<K, V> First => _innerMap.First;

        public string Id => _innerMap.Id;

        public new bool IsEmpty => _innerMap.IsEmpty;

        public override bool IsIndexed => _innerMap.IsIndexed;

        public override bool IsReadOnly => _innerMap.IsReadOnly;

        public new IEnumerable<K> Keys => _innerMap.Keys;

        public new KeyValuePair<K, V> Last => _innerMap.Last;

        public new object SyncRoot => _innerMap.SyncRoot;

        public new IEnumerable<V> Values => _innerMap.Values;

        internal bool IsWriter
        {
            get { return _isWriter; }
            set
            {
                _isWriter = value;
                var outer = (_innerMap as SortedChunkedMap<K, V>)?.OuterMap as RemoteChunksSeries<K, V>;
                if (outer != null) outer.ReadOnly = !_isWriter;
            }
        }


        public V GetAt(int idx) {
            return _innerMap.GetAt(idx);
        }

        public override ICursor<K, V> GetCursor() {
            return _innerMap.GetCursor();
        }

        public IAsyncEnumerator<KeyValuePair<K, V>> GetEnumerator() {
            return _innerMap.GetEnumerator();
        }

        public new bool TryFind(K key, Lookup direction, out KeyValuePair<K, V> value) {
            return _innerMap.TryFind(key, direction, out value);
        }

        public new bool TryGetFirst(out KeyValuePair<K, V> value) {
            return _innerMap.TryGetFirst(out value);
        }

        public new bool TryGetLast(out KeyValuePair<K, V> value) {
            return _innerMap.TryGetLast(out value);
        }

        public new bool TryGetValue(K key, out V value) {
            return _innerMap.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return (_innerMap as IEnumerable).GetEnumerator();
        }

        IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() {
            return (_innerMap as IEnumerable<KeyValuePair<K, V>>).GetEnumerator();
        }

    }
}