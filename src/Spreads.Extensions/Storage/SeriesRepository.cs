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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Spreads.Collections;
using Spreads.Collections.Persistent;
using Spreads.Serialization;

namespace Spreads.Storage {


    internal interface IAcceptCommand {
        void ApplyCommand(IntPtr pointer, int length);
        long Version { get; }
    }


    public class SeriesRepository : ISeriesRepository {
        // persistent dictionary to store all open series with write access and process id
        // we use single writer and steal locks if a writer process id is not among running processes.
        private PersistentMapFixedLength<long, int> _writeSeriesLocks;
        private const string WriteLocksFileName = "writelocks.pm";
        // not sure if that is needed
        //private PersistentMapFixedLength<long, int> _readerCount;

        private ILogBuffer _logBuffer;
        private const string LogBufferFileName = "logbuffer.lb";
        private const uint MinimumBufferSize = 10;
        private SeriesStorage _storage;
        private const string StorageFileName = "storage.db";


        // Opened series that could accept commands
        private readonly ConcurrentDictionary<UUID, IAcceptCommand> _openSeries = new ConcurrentDictionary<UUID, IAcceptCommand>();

        /// <summary>
        /// Create a series repository at a specified location.
        /// </summary>
        /// <param name="path">A directory path where repository is stored. If null or empty, then
        /// a default folder is used.</param>
        /// <param name="bufferSizeMb">Buffer size in megabytes. Ignored if below default.</param>
        public SeriesRepository(string path = null, uint bufferSizeMb = 10) {
            if (string.IsNullOrWhiteSpace(path)) {
                path = Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "Repos", "Default");
            } else if (!Path.IsPathRooted(path)) {
                path = Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "Repos", path);
            }
            var writeLocksFileName = Path.Combine(path, WriteLocksFileName);
            _writeSeriesLocks = new PersistentMapFixedLength<long, int>(writeLocksFileName, 1000);
            var logBufferFileName = Path.Combine(path, LogBufferFileName);
            _logBuffer = new LogBuffer(logBufferFileName, bufferSizeMb < MinimumBufferSize ? (int)MinimumBufferSize : (int)bufferSizeMb);
            var storageFileName = Path.Combine(path, StorageFileName);
            _storage = new SeriesStorageWrapper(_logBuffer, SeriesStorage.GetDefaultConnectionString(storageFileName));

            _logBuffer.OnAppend += _logBuffer_OnAppend;
        }

        private unsafe void _logBuffer_OnAppend(IntPtr pointer, int length) {
            var uuid = (*(CommandHeader*)(pointer + 4)).SeriesId;
            IAcceptCommand series;
            if (_openSeries.TryGetValue(uuid, out series)) {
                series.ApplyCommand(pointer, length);
            }
        }

        public async Task<PersistentSeries<K, V>> WriteSeries<K, V>(string seriesId, bool allowBatches = false) {
            // TODO extended id type check
            var uuid = new UUID(seriesId);
            throw new NotImplementedException();
        }

        public async Task<Series<K, V>> ReadSeries<K, V>(string seriesId) {
            // TODO extended id type check
            var uuid = new UUID(seriesId);
            IAcceptCommand series;
            if (_openSeries.TryGetValue(uuid, out series)) {
                return ((Series<K, V>)series).ReadOnly();
            }
            // TODO subscribe, wait for flush
            throw new NotImplementedException();
        }
    }



    // TODO storage wrapper
    internal sealed class SeriesStorageWrapper : SeriesStorage {
        private readonly ILogBuffer _logBuffer;

        public SeriesStorageWrapper(ILogBuffer logBuffer, string sqLiteConnectionString) : base(sqLiteConnectionString) {
            _logBuffer = logBuffer;
        }

        internal override Task<long> SaveChunk(SeriesChunk chunk) {


            //// NB lock is still needed here
            //    var extSid = this.GetExtendedSeriesId(chunk.Id);
            //    var mutationDto = new CommandDto {
            //        ExtendedSeriesId = extSid.ToString(),
            //        MutationType = (int)CommandType.SetChunk,
            //        SeriesVersion = chunk.Version,
            //        Payload = Serializer.Serialize(chunk)
            //    };
            //    //Trace.Assert(Serializer.Deserialize<SeriesChunk>(CommandDto.Payload).Version == chunk.Version);
            //    // TODO drop unsent individual mutations before this chunk
            //    _outMutationsQueue.Add(mutationDto);

            return base.SaveChunk(chunk);
        }


        internal override Task<long> RemoveChunk(long mapId, long key, long version, Lookup direction) {
            //var extSid = this.GetExtendedSeriesId(mapId);
            //var mutationDto = new CommandDto {
            //    ExtendedSeriesId = extSid.ToString(),
            //    MutationType = (int)CommandType.RemoveChunks,
            //    SeriesVersion = version,
            //    Payload = Serializer.Serialize(new RemoveChunksCommand { ChunkKey = key, lookup = direction })
            //};
            //_outMutationsQueue.Add(mutationDto);

            return base.RemoveChunk(mapId, key, version, direction);
        }
    }

    /// <summary>
    /// Series that could be written to.
    /// </summary>
    public sealed class PersistentSeries<K, V> : Series<K, V>, IPersistentOrderedMap<K, V>, IAcceptCommand {
        private readonly ILogBuffer _logBuffer;
        private readonly UUID _uuid;
        private readonly IPersistentOrderedMap<K, V> _innerMap;
        private readonly bool _allowBatches;
        private readonly bool _writer;
        private readonly Action _disposeCallback;

        internal PersistentSeries(ILogBuffer logBuffer, UUID uuid, IPersistentOrderedMap<K, V> innerMap, bool allowBatches, bool writer, Action disposeCallback = null) {
            _logBuffer = logBuffer;
            _uuid = uuid;
            _innerMap = innerMap;
            _allowBatches = allowBatches;
            _writer = writer;
            _disposeCallback = disposeCallback;

            if (TypeHelper<SetRemoveCommandBody<K, V>>.Size == -1) {
                throw new NotImplementedException();
            }
        }
        internal bool Writer => _writer;
        // NB we apply commands only to instantiated series when runtime does all types magic
        public unsafe void ApplyCommand(IntPtr pointer, int length) {
            // TODO do not log this commands
            var header = *(CommandHeader*)(pointer + 4);
            Trace.Assert(header.SeriesId == _uuid);
            var type = header.CommandType;
            switch (type) {
                case CommandType.Set:
                    // If this is a writer, it should ignore its own commands read from log-buffer
                    if (!_writer) {
                        var setBody =
                        TypeHelper<SetRemoveCommandBody<K, V>>.PtrToStructure(pointer + 4 + CommandHeader.Size);
                        _innerMap[setBody.key] = setBody.value;
                    }

                    break;
                case CommandType.Remove:
                    if (!_writer) {
                        var removeBody =
                            TypeHelper<SetRemoveCommandBody<K, int>>.PtrToStructure(pointer + 4 + CommandHeader.Size);
                        _innerMap.RemoveMany(removeBody.key, (Lookup)removeBody.value);
                    }
                    break;
                case CommandType.Append:
                    if (!_writer) {
                        throw new NotImplementedException();
                    }
                    break;
                case CommandType.Complete:
                    Trace.Assert(!_writer, "Writer should not receive data commands");
                    _innerMap.Complete();
                    break;
                case CommandType.SetChunk:
                    //var setChunkMutation = Serializer.Deserialize<SeriesChunk>(commandDto.Payload); {
                    //    var scm = _innerMap as SortedChunkedMap<K, V>;
                    //    var comparer = scm.Comparer as IKeyComparer<K>;
                    //    if (scm != null && comparer != null) {
                    //        var k = comparer.Add(default(K), setChunkMutation.ChunkKey);
                    //        var sm = Serializer.Deserialize<SortedMap<K, V>>(setChunkMutation.ChunkValue);
                    //        scm.OuterMap.Version = scm.OuterMap.Version - 1;
                    //        scm.OuterMap[k] = sm;
                    //    }
                    //}
                    break;
                case CommandType.RemoveChunk:
                    //var removeChunksMutation = Serializer.Deserialize<RemoveChunksCommand>(commandDto.Payload); {
                    //    var scm = _innerMap as SortedChunkedMap<K, V>;
                    //    var comparer = scm.Comparer as IKeyComparer<K>;
                    //    if (scm != null && comparer != null) {
                    //        var k = comparer.Add(default(K), removeChunksMutation.ChunkKey);
                    //        scm.OuterMap.RemoveMany(k, removeChunksMutation.lookup);
                    //    }
                    //}
                    break;

                case CommandType.Flush:
                    Trace.Assert(header.Version == _innerMap.Version);
                    break;

                case CommandType.Subscribe:
                    // if we are the single writer, we must flush so that new subscribers could see unsaved data
                    // if we are read-only
                    if (_writer) this.Flush();
                    break;

                default:
                    throw new ArgumentOutOfRangeException("Explicitly ignore all irrelevant cases here");
            }
            if (_innerMap.Version != header.Version) {
                Trace.TraceWarning("_innerMap.Version != Command header version");
            }
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
            var ptr = _logBuffer.Claim(len);
            *(CommandHeader*)(ptr + 4) = header;
            TypeHelper<SetRemoveCommandBody<K, V>>.StructureToPtr(commandBody, ptr + 4 + CommandHeader.Size);
            *(int*)ptr = len;
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
            //lock (_node._syncRoot) {
            //    var count = _innerMap.Append(appendMap, option);

            //    var appendMutation = new AppendCommand<K, V> {
            //        sortedMap = appendMap.ToSortedMap(),
            //        appendOption = option
            //    };

            //    var mutation = new CommandDto {
            //        //Id = _node.IncrementCounter(1),
            //        ExtendedSeriesId = _extendedSeriesId,
            //        MutationType = (int)CommandType.Append,
            //        SeriesVersion = _innerMap.Version,
            //        Payload = Serializer.Serialize(appendMutation)
            //    };
            //    _node._outMutationsQueue.Add(mutation);
            //    return count;
            //}
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
            var ptr = _logBuffer.Claim(len);
            *(CommandHeader*)(ptr + 4) = header;
            TypeHelper<SetRemoveCommandBody<K, int>>.StructureToPtr(commandBody, ptr + 4 + CommandHeader.Size);
            *(int*)ptr = len;
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
            var ptr = _logBuffer.Claim(len);
            *(CommandHeader*)(ptr + 4) = header;
            *(int*)ptr = len;
        }

        public void Complete() {
            _innerMap.Complete();
            _innerMap.Version = _innerMap.Version + 1;
            LogHeaderOnly(CommandType.Complete);
        }

        public void Flush() {
            _innerMap.Flush();
            LogHeaderOnly(CommandType.Flush);
        }

        private void Dispose(bool disposing) {
            if (_writer) Flush();
            _innerMap.Dispose();
            _disposeCallback?.Invoke();
            if (disposing) GC.SuppressFinalize(this);
        }

        public void Dispose() {
            Dispose(true);
        }

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