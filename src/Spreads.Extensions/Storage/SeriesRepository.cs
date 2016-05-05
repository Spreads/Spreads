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
using System.IO;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage {

    

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

        private unsafe void _logBuffer_OnAppend(IntPtr pointer) {
            var uuid = (*(CommandHeader*)(pointer + 4)).SeriesId;
            IAcceptCommand series;
            if (_openSeries.TryGetValue(uuid, out series)) {
                series.ApplyCommand(pointer);
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


        private sealed class SeriesStorageWrapper : SeriesStorage {
            private readonly ILogBuffer _logBuffer;

            public SeriesStorageWrapper(ILogBuffer logBuffer, string sqLiteConnectionString) : base(sqLiteConnectionString) {
                _logBuffer = logBuffer;
                throw new NotImplementedException("TODO storage wrapper");
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

        internal interface IAcceptCommand {
            void ApplyCommand(IntPtr pointer);
        }
    }



    
}