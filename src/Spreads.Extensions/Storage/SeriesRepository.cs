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
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage {


    public class SeriesRepository : ISeriesRepository, IDisposable {
        // persistent dictionary to store all open series with write access and process id
        // we use single writer and steal locks if a writer process id is not among running processes.
        private PersistentMapFixedLength<UUID, int> _writeSeriesLocks;
        private const string WriteLocksFileName = "writelocks";

        private ILogBuffer _logBuffer;
        private const string LogBufferFileName = "logbuffer";
        private const uint MinimumBufferSize = 10;
        private SeriesStorage _storage;
        private const string StorageFileName = "storage";

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
            }
            //else if (!Path.IsPathRooted(path)) {
            //    path = Path.Combine(Bootstrap.Bootstrapper.Instance.DataFolder, "Repos", path);
            //}
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
            var writeLocksFileName = Path.Combine(path, WriteLocksFileName);
            _writeSeriesLocks = new PersistentMapFixedLength<UUID, int>(writeLocksFileName, 1000);
            var logBufferFileName = Path.Combine(path, LogBufferFileName);
            _logBuffer = new LogBuffer(logBufferFileName, bufferSizeMb < MinimumBufferSize ? (int)MinimumBufferSize : (int)bufferSizeMb);
            var storageFileName = Path.Combine(path, StorageFileName);
            _storage = new SeriesStorageWrapper(_logBuffer, SeriesStorage.GetDefaultConnectionString(storageFileName));

            _logBuffer.OnAppend += _logBuffer_OnAppend;
        }


        public SeriesStorage Storage => _storage;
        private static readonly int Pid = Process.GetCurrentProcess().Id;

        private unsafe void _logBuffer_OnAppend(IntPtr pointer) {
            var uuid = (*(CommandHeader*)(pointer + 4)).SeriesId;
            IAcceptCommand series;
            if (_openSeries.TryGetValue(uuid, out series)) {
                series.ApplyCommand(pointer);
            }
        }

        public async Task<PersistentSeries<K, V>> WriteSeries<K, V>(string seriesId, bool allowBatches = false) {
            return await GetSeries<K, V>(seriesId, true, false);
        }

        public async Task<Series<K, V>> ReadSeries<K, V>(string seriesId) {
            return (await GetSeries<K, V>(seriesId, false, false)).ReadOnly();
        }


        private void Downgrade(UUID seriesId) {
            _writeSeriesLocks.Remove(seriesId);
            LogReleaseLock(seriesId);
        }

        private async Task Upgrade<K, V>(UUID seriesId, PersistentSeries<K, V> series) {
            while (true) {
                var released = await series.LockReleaseEvent.WaitAsync(1000);
                if (released) {
                    _writeSeriesLocks.Add(seriesId, Pid);
                    series.IsWriter = true;
                    LogAcquireLock(seriesId, series.Version);
                    break;
                } else
                {
                    int pid;
                    if (_writeSeriesLocks.TryGetValue(seriesId, out pid))
                    {
                        try {
                            Process.GetProcessById(pid);
                        } catch (ArgumentException) {
                            // pid is not running anymore, steal lock
                            Trace.TraceWarning($"Current process {Pid} has stolen a lock left by a dead process {pid}. If you see this often then dispose SeriesRepository properly before an application exit.");
                            _writeSeriesLocks[seriesId] = Pid;
                            series.IsWriter = true;
                            LogAcquireLock(seriesId, series.Version);
                            break;
                        }
                    }
                }
            }
        }

        private async Task<PersistentSeries<K, V>> GetSeries<K, V>(string seriesId, bool isWriter, bool allowBatches = false) {

            seriesId = seriesId.ToLowerInvariant().Trim();
            var exSeriesId = _storage.GetExtendedSeriesId<K, V>(seriesId);
            var uuid = new UUID(exSeriesId.UUID);
            IAcceptCommand series;
            if (_openSeries.TryGetValue(uuid, out series)) {
                var ps = (PersistentSeries<K, V>)series;
                ps.RefCounter++;
                return ps;
            }

            var disposeCallback = isWriter ? new Action(() => Downgrade(uuid)) : null;

            var waitFlush = new TaskCompletionSource<int>();
            var pSeries = new PersistentSeries<K, V>(_logBuffer, uuid,
                _storage.GetPersistentOrderedMap<K, V>(seriesId, false), allowBatches, isWriter,
                disposeCallback, waitFlush);
            pSeries.RefCounter++;
            _openSeries[uuid] = pSeries;

            LogSubscribe(uuid, pSeries.Version);

            if (isWriter) {
                try {
                    _writeSeriesLocks.Add(uuid, Pid);
                    LogAcquireLock(uuid, pSeries.Version);
                } catch (ArgumentException) {
                    await Upgrade(uuid, pSeries);
                }
            } else {
                // wait for flush if there is a writer 
                if (_writeSeriesLocks.ContainsKey(uuid)) {
                    await waitFlush.Task;
                }
            }
            return pSeries;
        }

        private unsafe void LogSubscribe(UUID uuid, long version) {
            var header = new CommandHeader {
                SeriesId = uuid,
                CommandType = CommandType.Subscribe,
                Version = version
            };
            var len = CommandHeader.Size;
            var ptr = _logBuffer.Claim(len);
            *(CommandHeader*)(ptr + 4) = header;
            *(int*)ptr = len;
        }

        private unsafe void LogAcquireLock(UUID uuid, long version) {
            var header = new CommandHeader {
                SeriesId = uuid,
                CommandType = CommandType.AcquireLock,
                Version = version
            };
            var len = CommandHeader.Size;
            var ptr = _logBuffer.Claim(len);
            *(CommandHeader*)(ptr + 4) = header;
            *(int*)ptr = len;
        }

        private unsafe void LogReleaseLock(UUID uuid) {
            var header = new CommandHeader {
                SeriesId = uuid,
                CommandType = CommandType.ReleaseLock,
            };
            var len = CommandHeader.Size;
            var ptr = _logBuffer.Claim(len);
            *(CommandHeader*)(ptr + 4) = header;
            *(int*)ptr = len;
        }

        private sealed class SeriesStorageWrapper : SeriesStorage {
            private readonly ILogBuffer _logBuffer;

            public SeriesStorageWrapper(ILogBuffer logBuffer, string sqLiteConnectionString) : base(sqLiteConnectionString) {
                _logBuffer = logBuffer;
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
                var ptr = _logBuffer.Claim(len);
                *(CommandHeader*)(ptr + 4) = header;
                TypeHelper<ChunkCommandBody>.StructureToPtr(commandBody, ptr + 4 + CommandHeader.Size);
                *(int*)ptr = len;

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
                var ptr = _logBuffer.Claim(len);
                *(CommandHeader*)(ptr + 4) = header;
                TypeHelper<ChunkCommandBody>.StructureToPtr(commandBody, ptr + 4 + CommandHeader.Size);
                *(int*)ptr = len;

                return ret;
            }
        }

        internal interface IAcceptCommand : IDisposable {
            void ApplyCommand(IntPtr pointer);
        }

        private void Dispose(bool disposing) {
            foreach (var series in _openSeries.Values) {
                series.Dispose();
            }
            if (disposing) {
                GC.SuppressFinalize(this);
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        ~SeriesRepository() {
            Dispose(false);
        }
    }




}