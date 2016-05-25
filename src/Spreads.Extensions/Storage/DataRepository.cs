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


    // NB for each series id each repository instance keeps a single series instance

    public class DataRepository : SeriesStorage, IDataRepository, IDisposable {
        // persistent dictionary to store all open series with write access and process id
        // we use single writer and steal locks if a writer process id is not among running processes.
        private readonly PersistentMapFixedLength<UUID, int> _writeSeriesLocks;
        private const string WriteLocksFileName = "writelocks";

        private readonly AppendLog _appendLog;
        private const string LogBufferFileName = "appendbuffer";
        private const uint MinimumBufferSize = 10;
        private const string StorageFileName = "chunkstorage";

        // Opened series that could accept commands
        private readonly ConcurrentDictionary<UUID, IAcceptCommand> _openSeries = new ConcurrentDictionary<UUID, IAcceptCommand>();
        private static short _counter = 0;
        private int _pid;
        private string _mapsPath;

        static DataRepository() {

        }

        /// <summary>
        /// Create a series repository at a specified location.
        /// </summary>
        /// <param name="path">A directory path where repository is stored. If null or empty, then
        /// a default folder is used.</param>
        /// <param name="bufferSizeMb">Buffer size in megabytes. Ignored if below default.</param>
        public DataRepository(string path = null, uint bufferSizeMb = 100) : base(GetConnectionStringFromPath(path)) {
            //if (!Path.IsPathRooted(path)) {
            //    path = Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "Repos", path);
            //}
            var seriesPath = Path.Combine(path, "series");
            _mapsPath = Path.Combine(path, "maps");
            if (!Directory.Exists(_mapsPath)) {
                Directory.CreateDirectory(_mapsPath);
            }
            var writeLocksFileName = Path.Combine(seriesPath, WriteLocksFileName);
            _writeSeriesLocks = new PersistentMapFixedLength<UUID, int>(writeLocksFileName, 1000);
            var logBufferFileName = Path.Combine(seriesPath, LogBufferFileName);
            _appendLog = new AppendLog(logBufferFileName, bufferSizeMb < MinimumBufferSize ? (int)MinimumBufferSize : (int)bufferSizeMb);
            _appendLog.OnAppend += OnLogAppend;
            _pid = (_counter << 16) | Process.GetCurrentProcess().Id;
            _counter++;
        }

        protected PersistentMapFixedLength<UUID, int> WriteSeriesLocks => _writeSeriesLocks;

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
        protected int Pid => _pid;

        public event OnAppendHandler OnBroadcast;

        protected virtual unsafe void OnLogAppend(DirectBuffer buffer) {


            var writerPid = buffer.ReadInt64(DataHeaderFlyweight.RESERVED_VALUE_OFFSET);
            var messageBuffer = new DirectBuffer(buffer.Length - DataHeaderFlyweight.HEADER_LENGTH,
                buffer.Data + DataHeaderFlyweight.HEADER_LENGTH);
            var header = *(CommandHeader*)(messageBuffer.Data);
            var uuid = header.SeriesId;

            IAcceptCommand series;
            if (
                writerPid != Pid // ignore our own messages
                &&
                _openSeries.TryGetValue(uuid, out series) // ignore messages for closed series
                ) {
                if (header.CommandType == CommandType.Broadcast) {
                    OnBroadcast?.Invoke(messageBuffer);
                } else {
                    series.ApplyCommand(messageBuffer);
                }
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


        private void Downgrade(UUID seriesId) {
            _writeSeriesLocks.Remove(seriesId);
            LogReleaseLock(seriesId);
        }

        private async Task Upgrade<K, V>(UUID seriesId, PersistentSeries<K, V> series) {
            if (!series.IsWriter) {
                try {
                    _writeSeriesLocks.Add(seriesId, Pid);
                    series.IsWriter = true;
                    LogAcquireLock(seriesId, series.Version);
                    return;
                } catch (ArgumentException) { }
            }
            while (true) {
                // wait if a writer from the same repo releases lock
                var released = await series.LockReleaseEvent.WaitAsync(1000);
                if (released) {
                    try {
                        // TODO TryAdd atomic method
                        _writeSeriesLocks.Add(seriesId, Pid);
                        series.IsWriter = true;
                        LogAcquireLock(seriesId, series.Version);
                        break;
                    } catch (ArgumentException) {
                        Trace.WriteLine("Could not upgrade after lock release, some one jumped ahead of us");
                    }
                } else {
                    int pid;
                    if (_writeSeriesLocks.TryGetValue(seriesId, out pid)) {
                        try {
                            Process.GetProcessById(pid & ((1 << 16) - 1));
                            Trace.TraceWarning("Tried to steal a lock but the owner process was alive.");
                        } catch (ArgumentException) {
                            // pid is not running anymore, steal lock
                            Trace.TraceWarning($"Current process {Pid} has stolen a lock left by a dead process {pid}. If you see this often then dispose SeriesRepository properly before application exit.");
                            _writeSeriesLocks[seriesId] = Pid;
                            series.IsWriter = true;
                            LogAcquireLock(seriesId, series.Version);
                            break;
                        }
                    }
                }
            }
        }

        protected virtual async Task<PersistentSeries<K, V>> GetSeries<K, V>(string seriesId, bool isWriter, bool allowBatches = false) {

            seriesId = seriesId.ToLowerInvariant().Trim();
            var exSeriesId = GetExtendedSeriesId<K, V>(seriesId);
            var uuid = new UUID(exSeriesId.UUID);
            IAcceptCommand series;
            if (_openSeries.TryGetValue(uuid, out series)) {
                var ps = (PersistentSeries<K, V>)series;
                Interlocked.Increment(ref ps.RefCounter);
                if (isWriter && !ps.IsWriter) {
                    await Upgrade(uuid, ps);
                }
                return ps;
            }

            Action<bool, bool> disposeCallback = (remove, downgrade) => {
                IAcceptCommand temp;
                if (remove) {
                    // NB this callback is called from temp.Dispose();
                    var removed = _openSeries.TryRemove(uuid, out temp);
                    Trace.Assert(removed);
                }
                if (downgrade) Downgrade(uuid);
            };

            var ipom = base.GetSeries<K, V>(exSeriesId.Id, exSeriesId.Version, false);
            var pSeries = new PersistentSeries<K, V>(_appendLog, _pid, uuid,
                ipom, allowBatches, isWriter,
                disposeCallback);
            // NB this is done in consturctor: pSeries.RefCounter++;
            _openSeries[uuid] = pSeries;

            LogSubscribe(uuid, pSeries.Version, exSeriesId.ToString());

            if (isWriter) {
                try {
                    _writeSeriesLocks.Add(uuid, Pid);
                    LogAcquireLock(uuid, pSeries.Version);
                } catch (ArgumentException) {
                    // NB do not wait // await Upgrade(uuid, pSeries);
                    throw new InvalidOperationException("Series is already opened for write. Only single writer is allowed.");
                }
            } else {
                // wait for flush if there is a live writer 
                int pid;
                if (_writeSeriesLocks.TryGetValue(uuid, out pid)) {
                    try {
                        Process.GetProcessById(pid & ((1 << 16) - 1));
                        await pSeries.FlushEvent.WaitAsync(-1);
                    } catch (ArgumentException) {
                        // pid is not running anymore, steal lock
                        Trace.TraceWarning($"Current process {Pid} has removed a lock left by a dead process {pid}. If you see this often then dispose SeriesRepository properly before application exit.");
                        _writeSeriesLocks.Remove(uuid);
                        LogReleaseLock(uuid);
                    }
                }
            }
            return pSeries;
        }

        public unsafe void Broadcast(DirectBuffer buffer, UUID correlationId = default(UUID),  long version = 0L) {
            var header = new CommandHeader {
                SeriesId = correlationId,
                CommandType = CommandType.Broadcast,
                Version = version
            };

            var len = CommandHeader.Size + (int)buffer.Length;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            claim.Buffer.WriteBytes(claim.Offset + CommandHeader.Size, buffer, 0, buffer.Length);
            claim.ReservedValue = Pid;
            claim.Commit();
        }


        private unsafe void LogSubscribe(UUID uuid, long version, string extendedTextId) {
            var header = new CommandHeader {
                SeriesId = uuid,
                CommandType = CommandType.Subscribe,
                Version = version
            };

            var textIdBytes = Encoding.UTF8.GetBytes(extendedTextId);

            var len = CommandHeader.Size + textIdBytes.Length;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            claim.Buffer.WriteBytes(claim.Offset + CommandHeader.Size, textIdBytes, 0, textIdBytes.Length);
            claim.ReservedValue = Pid;
            claim.Commit();
        }

        private unsafe void LogAcquireLock(UUID uuid, long version) {
            var header = new CommandHeader {
                SeriesId = uuid,
                CommandType = CommandType.AcquireLock,
                Version = version
            };
            var len = CommandHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            claim.ReservedValue = Pid;
            claim.Commit();
        }

        private unsafe void LogReleaseLock(UUID uuid) {
            var header = new CommandHeader {
                SeriesId = uuid,
                CommandType = CommandType.ReleaseLock,
            };
            var len = CommandHeader.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            claim.ReservedValue = Pid;
            claim.Commit();
        }


        internal unsafe override Task<long> SaveChunk(SeriesChunk chunk) {

            var ret = base.SaveChunk(chunk);

            var extSid = this.GetExtendedSeriesId(chunk.Id);
            var header = new CommandHeader {
                SeriesId = new UUID(extSid.UUID),
                CommandType = CommandType.SetChunk,
                Version = chunk.Version
            };
            var commandBody = new ChunkCommandBody {
                ChunkKey = chunk.ChunkKey,
                Count = chunk.Count,
            };
            var len = CommandHeader.Size + ChunkCommandBody.Size;


            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            TypeHelper<ChunkCommandBody>.StructureToPtr(commandBody, claim.Data + CommandHeader.Size);
            claim.ReservedValue = Pid;
            claim.Commit();

            return ret;
        }


        internal unsafe override Task<long> RemoveChunk(long mapId, long key, long version, Lookup direction) {
            var ret = base.RemoveChunk(mapId, key, version, direction);

            var extSid = this.GetExtendedSeriesId(mapId);
            var header = new CommandHeader {
                SeriesId = new UUID(extSid.UUID),
                CommandType = CommandType.RemoveChunk,
                Version = version
            };
            var commandBody = new ChunkCommandBody {
                ChunkKey = key,
                Lookup = (int)direction
            };
            var len = CommandHeader.Size + ChunkCommandBody.Size;
            BufferClaim claim;
            _appendLog.Claim(len, out claim);
            *(CommandHeader*)(claim.Data) = header;
            TypeHelper<ChunkCommandBody>.StructureToPtr(commandBody, claim.Data + CommandHeader.Size);
            claim.ReservedValue = Pid;
            claim.Commit();

            return ret;
        }



        internal interface IAcceptCommand : IDisposable {
            void ApplyCommand(DirectBuffer buffer);
        }

        protected virtual void Dispose(bool disposing) {
            foreach (var series in _openSeries.Values) {
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