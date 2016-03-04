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
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Data.Sqlite.Utilities;
using Spreads.Collections;

namespace Spreads.Storage {

    public class SeriesStorage : ISeriesStorage {
        private readonly SqliteConnection _connection;
        private readonly ConcurrentDictionary<long, object> _writableSeriesStore = new ConcurrentDictionary<long, object>();
        private readonly ConcurrentDictionary<long, object> _readOnlySeriesStore = new ConcurrentDictionary<long, object>();
        private readonly Func<long, long, Task<SortedMap<long, SeriesChunk>>> _remoteKeysLoader;
        private readonly Func<long, long, Task<SeriesChunk>> _remoteChunkLoader;


        public string IdTableName { get; }
        public string ChunkTableName { get; }

        

        private static SqliteConnection CreateConnection(string connectionString, bool async = false) {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            connection.ExecuteNonQuery("PRAGMA main.page_size = 4096; ");
            connection.ExecuteNonQuery("PRAGMA main.cache_size = 25000;");
            connection.ExecuteNonQuery(!async ? "PRAGMA synchronous = NORMAL;" : "PRAGMA synchronous = OFF;");
            connection.ExecuteNonQuery("PRAGMA journal_mode = WAL;");
            return connection;
        }

        private static readonly string _defaultPath = Bootstrap.Bootstrapper.Instance.DataFolder;
        private static readonly SeriesStorage _default = new SeriesStorage($"Filename={Path.Combine(_defaultPath, "default.db")}");
        public static SeriesStorage Default
        {
            get { return _default; }
        }

        public SqliteConnection Connection
        {
            get { return _connection; }
        }

        public SeriesStorage(string SQLiteConnectionString) : this(CreateConnection(SQLiteConnectionString, true)) {

        }

        private SeriesStorage(SqliteConnection connection, string idTableName = "spreads_series_id", string chunkTableName = "spreads_series_chunk") {
            _connection = connection;
            IdTableName = idTableName;
            ChunkTableName = chunkTableName;

            var createSeriesIdTable =
                $"CREATE TABLE IF NOT EXISTS `{IdTableName}` (\n" +
                "  `Id` INTEGER  PRIMARY KEY,\n" +
                "  `TextId` TEXT NOT NULL,\n" +
                "  `UUID` BLOB NOT NULL,\n" +
                "  `KeyType` TEXT NOT NULL,\n" +
                "  `ValueType` TEXT NOT NULL,\n" +
                "  CONSTRAINT `UX_TextId` UNIQUE (`TextId`)\n" +
                "  CONSTRAINT `UX_UUID` UNIQUE (`UUID`)\n" +
                ")";
            var createSeriesChunksTable =
                $"CREATE TABLE IF NOT EXISTS `{ChunkTableName}` (\n" +
                "  `Id` INTEGER NOT NULL,\n" +
                "  `ChunkKey` INTEGER NOT NULL,\n" +
                "  `Count` INTEGER NOT NULL,\n" +
                "  `Version` INTEGER NOT NULL,\n" +
                "  `ChunkValue` BLOB NOT NULL,\n" +
                "  PRIMARY KEY (`Id`,`ChunkKey`)\n)";

            _connection.Execute(createSeriesIdTable);
            _connection.Execute(createSeriesChunksTable);

            _remoteKeysLoader = (mapid, version) => {
                var sm = new SortedMap<long, SeriesChunk>();
                var chunks = _connection.Query<SeriesChunk>("SELECT Id, ChunkKey, Count, Version from " + ChunkTableName + "" + " WHERE Id = @Id ORDER BY ChunkKey", new { Id = mapid });
                foreach (var ch in chunks) {
                    sm.AddLast(ch.ChunkKey, ch);
                }
                return Task.FromResult(sm);
            };

            _remoteChunkLoader = (mapid, chunkId) => {
                var sod = _connection.Query<SeriesChunk>("SELECT Id, ChunkKey, ChunkValue, Count, Version from " + ChunkTableName + "" + " WHERE Id = @Id AND ChunkKey = @Dt", new { Dt = chunkId, Id = mapid }).SingleOrDefault();
                if (sod != null) {
                    return Task.FromResult(sod);
                }
                throw new KeyNotFoundException();
            };
        }

        private async void FlushAll() {
            foreach (var scm in _writableSeriesStore.Values.Select(obj => obj as IPersistentOrderedMap<DateTime, decimal>)) {
                await Task.Delay(250);
                scm?.Flush();
            }
        }


        public IPersistentOrderedMap<K, V> GetPersistentOrderedMap<K, V>(string seriesId, bool readOnly = false) {
            if (readOnly) {
                seriesId = seriesId.ToLowerInvariant().Trim();
                var id = GetLongId<K, V>(seriesId);
                return _readOnlySeriesStore.GetOrAdd(id, id2 => {
                    return GetSeries<K, V>(id2, true);
                }) as IPersistentOrderedMap<K, V>;
            } else {
                seriesId = seriesId.ToLowerInvariant().Trim();
                var id = GetLongId<K, V>(seriesId);
                return _writableSeriesStore.GetOrAdd(id, id2 => {
                    return GetSeries<K, V>(id2, false);
                }) as IPersistentOrderedMap<K, V>;
            }
        }


        private long GetLongId<K, V>(string seriesId) {
            seriesId = seriesId.ToLowerInvariant().Trim();
            var keyType = typeof(K).FullName;
            var valueType = typeof(V).FullName;

            var seriesIdRow = _connection.Query<SeriesId>("SELECT Id, TextId, UUID, KeyType, ValueType from " + IdTableName + "" + " WHERE TextId = @TextId", new { TextId = seriesId }, buffered: false).SingleOrDefault();
            if (seriesIdRow != null) {
                if (seriesIdRow.KeyType != keyType || seriesIdRow.ValueType != valueType) {
                    throw new ArgumentException(
                        $"Wrong types for {seriesId}: expexting <{seriesIdRow.KeyType},{seriesIdRow.ValueType}> but requested <{keyType},{valueType}>");
                }
                return seriesIdRow.Id;
            }

            var setSql = @"INSERT INTO " + IdTableName + " (TextId, UUID, KeyType, ValueType) VALUES ( @TextId, @UUID, @KeyType, @ValueType ); " + "SELECT Id from " + IdTableName + " WHERE TextId = @TextId;";
            var id = _connection.ExecuteScalar<long>(setSql, new {
                TextId = seriesId,
                UUID = seriesId.MD5Bytes(),
                KeyType = keyType,
                ValueType = valueType
            });
            return id;
        }

        // assert consistency
        internal delegate void ChunkSaveHandler(SeriesChunk chunk);
        internal event ChunkSaveHandler OnChunkSave;
        internal bool AssertChunkSet<K, V>(SeriesChunk chunk)
        {
            if (!_readOnlySeriesStore.ContainsKey(chunk.Id)) return false;
            var outer =
                (_readOnlySeriesStore[chunk.Id] as SortedChunkedMap<K, V>)?.outerMap as RemoteChunksSeries<K, V>;
            if (outer == null) return false;
            //var k = outer.FromInt64(chunk.ChunkKey);
            try
            {
                var lv = outer._chunksCache[chunk.ChunkKey];
                return lv.ChunkSize == chunk.Count && lv.ChunkVersion == chunk.Version;
            }
            catch (KeyNotFoundException e)
            {
                return false;
            }
            return false;
        }

        private SortedChunkedMap<K, V> GetSeries<K, V>(long seriesId, bool readOnly, int chunkSize = 4096) {
            SortedChunkedMap<K, V> series;
            var comparer = KeyComparer.GetDefault<K>() as IKeyComparer<K>;
            if (comparer != null) {

                //// mapId, chunkKey, deserialied chunk => whole map version
                Func<SeriesChunk, Task<long>> remoteSaver = chunk => {

                    var setSQL = @"INSERT OR REPLACE INTO " + ChunkTableName + " (Id,ChunkKey,Count,Version,ChunkValue)" + " VALUES ( @id, @chKey, @count, @version, @chVal)";
                    var processedTmp = _connection.Execute(setSQL, new {
                        id = chunk.Id,
                        chKey = chunk.ChunkKey,
                        count = chunk.Count,
                        version = chunk.Version,
                        chVal = chunk.ChunkValue
                    });
                    if (processedTmp < 1) throw new ApplicationException("Cannot set value");
                    OnChunkSave?.Invoke(chunk);
                    return Task.FromResult(0L);
                    //}
                };

                // mapId, chunkKey, direction => whole map version
                Func<long, long, Lookup, Task<long>> remoteRemover = (mapId, key, direction) => {
                    bool r2 = false;
                    string setSQL;
                    int processedTmp;
                    switch (direction) {
                        case Lookup.EQ:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" + " WHERE Id = @id AND ChunkKey = @dt LIMIT 1";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            if (processedTmp == 0) {
                                // ReSharper disable once RedundantAssignment
                                r2 = false;
                            } else if (processedTmp > 1) {
                                throw new ApplicationException("Deleted more than one row");
                            } else {
                                r2 = true;
                            }

                            break;
                        case Lookup.LT:

                            setSQL = @"DELETE FROM " + ChunkTableName + "" + " WHERE Id = @id AND ChunkKey < @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;

                            break;
                        case Lookup.LE:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" + " WHERE Id = @id AND ChunkKey <= @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;

                            break;
                        case Lookup.GE:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" + " WHERE Id = @id AND ChunkKey >= @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;

                            break;
                        case Lookup.GT:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" + " WHERE Id = @id AND ChunkKey > @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;
                            break;
                    }
                    // RemoteSeries treats positive as true
                    return r2 ? Task.FromResult(1L) : Task.FromResult(-1L);
                };

                Func<IComparer<K>, IOrderedMap<K, SortedMap<K, V>>> outerFactory =
                    cmp => new RemoteChunksSeries<K, V>(
                        seriesId,
                        cmp as IKeyComparer<K>,
                        _remoteKeysLoader,
                        _remoteChunkLoader,
                        remoteSaver,
                        remoteRemover, readOnly);

                series = new SortedChunkedMap<K, V>(outerFactory, comparer, chunkSize);
                // better be safe
                series.IsSynchronized = true;
                return series;
            }
            throw new NotSupportedException("Only type that have IKeyComparer<K> are supported");
        }

        public void Dispose() {
            FlushAll();
            _connection.Dispose();
        }

        ~SeriesStorage() {
            Dispose();
        }
    }
}
