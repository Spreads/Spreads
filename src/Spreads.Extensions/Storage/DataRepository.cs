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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Logbuffer;
using Spreads.Storage.Aeron.Protocol;

namespace Spreads.Storage {

    // TODO we are reinvernting Raft here with this conductor election and switching stuff,
    // so do not even try to make our version 100% correct - just make it work in the common case
    // and later (someday) implement Raft.

    // NB for each series id each repository instance keeps a single series instance

    public class DataRepository : SeriesStorage, IDataRepository, IDisposable {
        // persistent dictionary to store all open series with write access and process id
        // we use single writer and steal locks if a writer process id is not among running processes.
        private readonly PersistentMapFixedLength<UUID, long> _writeSeriesLocks;
        private const string WriteLocksFileName = "writelocks";

        private readonly AppendLog _appendLog;
        private const string LogBufferFileName = "appendbuffer";
        private const uint MinimumBufferSize = 10;
        private const string StorageFileName = "chunkstorage";

        // Opened objects that could accept commands
        private readonly ConcurrentDictionary<UUID, IAcceptCommand> _openStreams = new ConcurrentDictionary<UUID, IAcceptCommand>();

        private static int _counter = 0;
        private readonly long _pid;
        private readonly string _mapsPath;
        private readonly UUID _conductorLock = new UUID("conductor://conductor.lock");// just a magic number
        private bool _isConductor;

        private Dictionary<UUID, TaskCompletionSource<long>> _writerRequestsOustanding =
            new Dictionary<UUID, TaskCompletionSource<long>>();
        private Dictionary<UUID, TaskCompletionSource<byte>> _syncRequestsOustanding =
            new Dictionary<UUID, TaskCompletionSource<byte>>();

        /// <summary>
        /// Create a series repository at a specified location.
        /// </summary>
        /// <param name="path">A directory path where repository is stored. If null or empty, then
        /// a default folder is used.</param>
        /// <param name="bufferSizeMb">Buffer size in megabytes. Ignored if below default.</param>
        public DataRepository(string path = null, uint bufferSizeMb = 100) : base(GetConnectionStringFromPath(path)) {
            if (string.IsNullOrWhiteSpace(path)) {
                path = Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "Repos", "Default");
            }
            var seriesPath = Path.Combine(path, "series");
            _mapsPath = Path.Combine(path, "maps");
            if (!Directory.Exists(_mapsPath)) {
                Directory.CreateDirectory(_mapsPath);
            }
            var writeLocksFileName = Path.Combine(seriesPath, WriteLocksFileName);
            _writeSeriesLocks = new PersistentMapFixedLength<UUID, long>(writeLocksFileName, 1000);
            var logBufferFileName = Path.Combine(seriesPath, LogBufferFileName);
            _appendLog = new AppendLog(logBufferFileName, bufferSizeMb < MinimumBufferSize ? (int)MinimumBufferSize : (int)bufferSizeMb);
            _appendLog.OnAppend += OnLogAppend;
            _pid = (((long)_counter) << 32) | (long)Process.GetCurrentProcess().Id;
            _counter++;

            _isConductor = TryBecomeConductor();
        }

        private static string GetConnectionStringFromPath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                path = Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "Repos", "Default", "series");
            }
            var seriesPath = Path.Combine(path, "series");
            if (!Directory.Exists(seriesPath)) {
                Directory.CreateDirectory(seriesPath);
            }
            var storageFileName = Path.Combine(seriesPath, StorageFileName);
            return SeriesStorage.GetDefaultConnectionString(storageFileName);
        }


        // fake a different Pid for each repo instance inside a single process
        protected long Pid => _pid;

        public PersistentMapFixedLength<UUID, long> WriteSeriesLocks => _writeSeriesLocks;

        internal bool IsConductor => _isConductor;

        /// <summary>
        /// Must call LogSynced(seriesId) when storage has all relevant data (asyncronously)
        /// </summary>
        protected virtual void BeginSyncronizeSeries(UUID seriesId, long version) {
            // if DataRepo is a conductor than there is no any external conductor and 
            // we have only data in the storage
            LogSynced(seriesId);
        }

        protected unsafe void OnLogAppend(DirectBuffer buffer) {

            var writerPid = buffer.ReadInt64(DataHeaderFlyweight.RESERVED_VALUE_OFFSET);
            var messageBuffer = new DirectBuffer(buffer.Length - DataHeaderFlyweight.HEADER_LENGTH,
                buffer.Data + DataHeaderFlyweight.HEADER_LENGTH);
            var header = *(MessageHeader*)(messageBuffer.Data);
            var seriesId = header.UUID;
            var messageType = header.MessageType;

            switch (messageType) {
                case MessageType.ConductorMessage:
                    // this message could be sent only by a process that
                    // thinks it is a conductor
                    var currentConductor = writerPid;
                    if (Math.Abs(currentConductor) != Pid) {
                        // negative writerPid must be sent on conductor repo dispose
                        _isConductor = writerPid <= 0 && TryBecomeConductor();
                    }
                    break;

                case MessageType.WriteRequest:
                    if (_isConductor) {
                        var currentWriter = TryAcquireWriteLockOrGetCurrent(writerPid, seriesId);
                        LogCurrentWriter(seriesId, currentWriter);
                    }
                    break;

                case MessageType.CurrentWriter:
                    // new writer is sent by a conductor in response 
                    TaskCompletionSource<long> temp;
                    if (_writerRequestsOustanding.TryGetValue(seriesId, out temp)) {
                        temp.SetResult(writerPid);
                    }
                    break;

                case MessageType.Subscribe:
                    if (_isConductor) {
                        IAcceptCommand tempAcceptCommand;
                        if (_openStreams.TryGetValue(seriesId, out tempAcceptCommand)) {
                            tempAcceptCommand.Flush();
                        }
                        BeginSyncronizeSeries(seriesId, header.Version);
                    }
                    break;

                case MessageType.Synced:
                    TaskCompletionSource<byte> temp2;
                    if (_syncRequestsOustanding.TryGetValue(seriesId, out temp2)) {
                        temp2.SetResult(1);
                    }
                    break;


                default:
                    IAcceptCommand acceptCommand;
                    if (writerPid != Pid // ignore our own messages
                        && _openStreams.TryGetValue(seriesId, out acceptCommand) // ignore messages for closed series
                        ) {
                        acceptCommand.ApplyCommand(messageBuffer);
                    }
                    break;
            }


        }

        public async Task<PersistentSeries<K, V>> WriteSeries<K, V>(string seriesId, bool allowBatches = false) {
            return await GetSeries<K, V>(seriesId, true, false);
        }

        public async Task<Series<K, V>> ReadSeries<K, V>(string seriesId) {
            return (await GetSeries<K, V>(seriesId, false, false)).ReadOnly();
        }

        public Task<IDictionary<K, V>> WriteMap<K, V>(string mapId, int initialCapacity) {
            if (mapId == null) throw new ArgumentNullException(nameof(mapId));
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            var mapPath = Path.Combine(_mapsPath, mapId);
            var map = PersistentMap<K, V>.Open(mapPath, initialCapacity);
            return Task.FromResult((IDictionary<K, V>)map);
        }

        /// <summary>
        /// Get ISubject (IObservable + IObserver) that allows to broadcast messages and receive
        /// messages from other broadcasters on the same channel.
        /// </summary>
        public Task<BroadcastObservable<T>> Broadcast<T>(string channelId) {
            var bo = new BroadcastObservable<T>(_appendLog, channelId, Pid);
            _openStreams[bo.UUID] = bo;
            return Task.FromResult(bo);
        }


        private void Downgrade(UUID seriesId) {
            if (_isConductor) {
                _writeSeriesLocks.Remove(seriesId);
            }
            LogReleaseLock(seriesId);
        }

        /// <summary>
        /// Try to acquire a lock for series. Used from WriteSeries()
        /// </summary>
        private async Task<bool> RequestWriteLock(UUID seriesId) {
            if (_isConductor) {
                return TryAcquireWriteLockOrGetCurrent(Pid, seriesId) == Pid;
            }

            var tcs = new TaskCompletionSource<long>();
            _writerRequestsOustanding.Add(seriesId, tcs);
            LogWriteRequest(seriesId);
            var currentWriter = await tcs.Task;
            return currentWriter == Pid;
        }

        private async Task RequestSubscribeSynced(UUID seriesId, long version, string extendedTextId) {
            var tcs = new TaskCompletionSource<byte>();
            _syncRequestsOustanding.Add(seriesId, tcs);
            LogSubscribe(seriesId, version, extendedTextId);
            await tcs.Task;
        }

        private bool IsProcessAlive(long pid) {
            try {
                Process.GetProcessById((int)(pid & ((1L << 32) - 1L)));
                return true;
            } catch (ArgumentException) {
                return false;
            }
        }

        private bool TryBecomeConductor() {
            try {
                _writeSeriesLocks.Add(_conductorLock, Pid);
            } catch (ArgumentException) {
            }
            long conductorPid;
            if (_writeSeriesLocks.TryGetValue(_conductorLock, out conductorPid)) {
                if (conductorPid == Pid) {
                    LogConductorPid(Pid);
                    return true;
                }
                if (IsProcessAlive(conductorPid)) return false;
                // TODO need atomic CAS operations on dictionary, however in this case it is not very important, we should not open/close repos many times
                _writeSeriesLocks[_conductorLock] = Pid;
                LogConductorPid(Pid);
                return true;
            }
            throw new ApplicationException("Value must exist");
        }

        private long TryAcquireWriteLockOrGetCurrent(long writerPid, UUID seriesId) {
            try {
                _writeSeriesLocks.Add(seriesId, writerPid);
            } catch (ArgumentException) {
            }
            long pid;
            if (_writeSeriesLocks.TryGetValue(seriesId, out pid)) {
                if (pid == writerPid) {
                    return pid;
                }
                if (IsProcessAlive(pid)) return pid;
                // TODO need atomic CAS operations on dictionary, however in this case it is not very important, we should not open/close repos many times
                _writeSeriesLocks[seriesId] = writerPid;
                return writerPid;
            }
            throw new ApplicationException("Value must exist");
        }


        protected virtual async Task<PersistentSeries<K, V>> GetSeries<K, V>(string seriesId, bool isWriter, bool allowBatches = false) {
            seriesId = seriesId.ToLowerInvariant().Trim();
            var exSeriesId = GetExtendedSeriesId<K, V>(seriesId);
            var uuid = new UUID(exSeriesId.UUID);
            IAcceptCommand series;
            if (_openStreams.TryGetValue(uuid, out series)) {
                var ps = (PersistentSeries<K, V>)series;
                Interlocked.Increment(ref ps.RefCounter);
                if (isWriter && !ps.IsWriter) {
                    var hasLockAcquired = await RequestWriteLock(uuid);
                    if (!hasLockAcquired) throw new SingleWriterException();
                    // Upgrade to writer
                    ps.IsWriter = true;
                }
                return ps;
            }

            // NB We restrict only opening more than once, once opened a series object could be modified by many threads

            if (isWriter) {
                var hasLockAcquired = await RequestWriteLock(uuid);
                if (!hasLockAcquired) throw new SingleWriterException();
            }

            // NB: Writers now have lock acquired. Other logic is the same for both writers and readers.

            Action<bool, bool> disposeCallback = (remove, downgrade) => {
                IAcceptCommand temp;
                if (remove) {
                    // NB this callback is called from temp.Dispose();
                    var removed = _openStreams.TryRemove(uuid, out temp);
                    Trace.Assert(removed);
                }
                if (downgrade) Downgrade(uuid);
            };

            var ipom = base.GetSeries<K, V>(exSeriesId.Id, exSeriesId.Version, false);
            var pSeries = new PersistentSeries<K, V>(_appendLog, _pid, uuid, ipom, allowBatches, isWriter, disposeCallback);
            // NB this is done in consturctor: pSeries.RefCounter++;
            _openStreams[uuid] = pSeries;

            await RequestSubscribeSynced(uuid, pSeries.Version, exSeriesId.ToString());

            return pSeries;
        }


        public unsafe void Broadcast(DirectBuffer buffer, UUID correlationId = default(UUID), long version = 0L) {
            var header = new MessageHeader {
                UUID = correlationId,
                MessageType = MessageType.Broadcast,
                Version = version
            };

            var len = MessageHeader.Size + (int)buffer.Length;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.Buffer.WriteBytes(claim.Offset + MessageHeader.Size, buffer, 0, buffer.Length);
            claim.ReservedValue = Pid;
            claim.Commit();
        }

        protected unsafe void LogConductorPid(long conductorPid) {
            var header = new MessageHeader {
                UUID = _conductorLock,
                MessageType = MessageType.ConductorMessage,
                Version = 0L
            };
            var len = MessageHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = conductorPid;
            claim.Commit();
        }

        protected unsafe void LogWriteRequest(UUID uuid) {
            var header = new MessageHeader {
                UUID = uuid,
                MessageType = MessageType.WriteRequest,
                Version = 0L
            };
            var len = MessageHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = Pid;
            claim.Commit();
        }

        protected unsafe void LogCurrentWriter(UUID uuid, long writerPid) {
            var header = new MessageHeader {
                UUID = uuid,
                MessageType = MessageType.CurrentWriter,
                Version = 0L
            };
            var len = MessageHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = writerPid;
            claim.Commit();
        }

        protected unsafe void LogSubscribe(UUID uuid, long version, string extendedTextId) {
            var header = new MessageHeader {
                UUID = uuid,
                MessageType = MessageType.Subscribe,
                Version = version
            };

            var textIdBytes = Encoding.UTF8.GetBytes(extendedTextId);

            var len = MessageHeader.Size + textIdBytes.Length;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.Buffer.WriteBytes(claim.Offset + MessageHeader.Size, textIdBytes, 0, textIdBytes.Length);
            claim.ReservedValue = Pid;
            claim.Commit();
        }

        protected unsafe void LogSynced(UUID uuid) {
            var header = new MessageHeader {
                UUID = uuid,
                MessageType = MessageType.Synced,
                Version = 0L
            };
            var len = MessageHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = Pid;
            claim.Commit();
        }

        protected unsafe void LogReleaseLock(UUID uuid) {
            var header = new MessageHeader {
                UUID = uuid,
                MessageType = MessageType.WriteRelease,
            };
            var len = MessageHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = Pid;
            claim.Commit();
        }


        internal override unsafe Task<long> SaveChunk(SeriesChunk chunk) {
            var ret = base.SaveChunk(chunk);

            var extSid = this.GetExtendedSeriesId(chunk.Id);
            var header = new MessageHeader {
                UUID = new UUID(extSid.UUID),
                MessageType = MessageType.SetChunk,
                Version = chunk.Version
            };
            var commandBody = new ChunkCommandBody {
                ChunkKey = chunk.ChunkKey,
                Count = chunk.Count,
            };
            var len = MessageHeader.Size + ChunkCommandBody.Size;


            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            TypeHelper<ChunkCommandBody>.StructureToPtr(commandBody, claim.Data + MessageHeader.Size);
            claim.ReservedValue = Pid;
            claim.Commit();

            return ret;
        }


        internal override unsafe Task<long> RemoveChunk(long mapId, long key, long version, Lookup direction) {
            var ret = base.RemoveChunk(mapId, key, version, direction);

            var extSid = this.GetExtendedSeriesId(mapId);
            var header = new MessageHeader {
                UUID = new UUID(extSid.UUID),
                MessageType = MessageType.RemoveChunk,
                Version = version
            };
            var commandBody = new ChunkCommandBody {
                ChunkKey = key,
                Lookup = (int)direction
            };
            var len = MessageHeader.Size + ChunkCommandBody.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            TypeHelper<ChunkCommandBody>.StructureToPtr(commandBody, claim.Data + MessageHeader.Size);
            claim.ReservedValue = Pid;
            claim.Commit();

            return ret;
        }


        internal interface IAcceptCommand : IPersistentObject {
            void ApplyCommand(DirectBuffer buffer);
        }

        protected virtual void Dispose(bool disposing) {
            // negative Pid to nitify other that this repo is disposing and was a conductor
            if (_isConductor)
            {
                _isConductor = false;
                _writeSeriesLocks.Remove(_conductorLock);
                LogConductorPid(-Pid);
            }
            foreach (var series in _openStreams.Values) {
                series.Dispose();
            }
            _appendLog.Dispose();
            _writeSeriesLocks.Dispose();
            base.Dispose();
            if (disposing) {
                GC.SuppressFinalize(this);
            }
            Trace.WriteLineIf(!disposing, "Disposing repo from finalizer");
        }

        public void Dispose() {
            Dispose(true);
        }

        ~DataRepository() {
            Dispose(false);
        }
    }
}