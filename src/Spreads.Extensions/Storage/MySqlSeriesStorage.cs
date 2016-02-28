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
using System.Diagnostics;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Spreads.Collections;
using Spreads.Serialization;

namespace Spreads.Storage {

    // TODO (low) need to adjust SQL for each major RDBMS. Won't do until use any other DB myself
    // Need an enum for DB type and SQL strings dict of something



    public class MySqlSeriesStorage : ISeriesStorage {
        

        private readonly IDbConnection _connection;

        private readonly System.Runtime.Caching.MemoryCache _cache;
        private readonly Dictionary<string, long> _knowdIds = new Dictionary<string, long>();
        private readonly ConcurrentDictionary<string, object> _seriesStore = new ConcurrentDictionary<string, object>();
        private Task _flusher;
        private readonly Func<long, long, Task<byte[]>> _remoteKeysLoaderBytes;
        private readonly Func<long, long, Task<byte[]>> _remoteChunkLoader;
        public string IdTableName { get; }
        public string ChunkTableName { get; }

        public MySqlSeriesStorage(IDbConnection connection, string idTableName = "spreads_series_id", string chunkTableName = "spreads_series_chunk") {
            _connection = connection;
            _cache = new System.Runtime.Caching.MemoryCache("MySqlSeriesStorage");
            _flusher = Task.Run(async () => {
                while (true) {
                    await Task.Delay(1000);
                    await FlushAll();
                }
            });

            IdTableName = idTableName;
            ChunkTableName = chunkTableName;

            var createSeriesIdTable =
                $"CREATE TABLE IF NOT EXISTS `{IdTableName}` (\n  `Id` bigint(20) NOT NULL AUTO_INCREMENT,\n  `TextId` varchar(255) NOT NULL,\n  PRIMARY KEY (`Id`),\n  UNIQUE KEY `IX_TextId` (`TextId`) USING HASH\n)";
            var createSeriesChunksTable =
                $"CREATE TABLE IF NOT EXISTS `{ChunkTableName}` (\n  `Id` bigint(20) NOT NULL,\n  `ChunkKey` bigint(20) NOT NULL,\n  `Count` int(11) NOT NULL,\n  `Version` int(11) NOT NULL,\n  `ChunkValue` longblob NOT NULL,\n  PRIMARY KEY (`Id`,`ChunkKey`)\n)";
            _connection.Execute(createSeriesIdTable);
            _connection.Execute(createSeriesChunksTable);

            _remoteKeysLoaderBytes = (mapid, version) => {
                var key = "keys:" + mapid.ToString();
                var cached = _cache.Get(key);
                if (cached != null) {
                    return Task.FromResult(Serializer.Serialize((cached as SortedMap<long, int>)));
                }

                var sm = new SortedMap<long, int>();

                var chunks = _connection.Query<SeriesChunk>("SELECT ChunkKey, Version from " + ChunkTableName + "" +
                                                     " WHERE Id = @Id ORDER BY ChunkKey",
                    new { Id = mapid });
                foreach (var ch in chunks) {
                    sm.AddLast(ch.ChunkKey, ch.Version);
                }

                var bytes = Serializer.Serialize(sm);
                return Task.FromResult(bytes);
            };

            _remoteChunkLoader = (mapid, chunkId) => {
                var sod = _connection.Query<SeriesChunk>("SELECT ChunkKey, ChunkValue, Count, Version from " + ChunkTableName + "" +
                                            " WHERE Id = @Id AND ChunkKey = @Dt",
                                            new { Dt = chunkId, Id = mapid }).SingleOrDefault();
                if (sod != null) {
                    return Task.FromResult(sod.ChunkValue);
                }
                throw new KeyNotFoundException();

            };
        }

        private async Task FlushAll() {
            foreach (var scm in _seriesStore.Values.Select(obj => obj as IPersistentOrderedMap<DateTime, decimal>)) {
                await Task.Delay(250);
                scm?.Flush();
            }
        }

        public IPersistentOrderedMap<K, V> GetPersistentOrderedMap<K, V>(string seriesId) {
            seriesId = seriesId.ToLowerInvariant().Trim();
            return _seriesStore.GetOrAdd(seriesId, (seriesId2) => {
                long id;
                id = GetLongId(seriesId2);
                return GetSeries<K, V>(id);
            }) as IPersistentOrderedMap<K, V>;
        }


        private long GetLongId(string seriesId) {
            if (_knowdIds.ContainsKey(seriesId)) {
                return _knowdIds[seriesId];
            }

            var seriesIdRow = _connection.Query<SeriesId>("SELECT Id, TextId from " + IdTableName + "" +
                                                         " WHERE TextId = @TextId",
                                                         new { TextId = seriesId }, buffered: false).SingleOrDefault();
            if (seriesIdRow != null) {
                _knowdIds[seriesId] = seriesIdRow.Id;
                return seriesIdRow.Id;
            }

            var setSQL = @"INSERT INTO " + IdTableName +
                         " (TextId) VALUES ( @TextId ); " +
                         "SELECT Id from " + IdTableName + " WHERE TextId = @TextId;";
            var id = _connection.ExecuteScalar<long>(setSQL,
                new {
                    TextId = seriesId
                });
            _knowdIds[seriesId] = id;
            return id;

        }


        private SortedChunkedMap<K, V> GetSeries<K, V>(long seriesId, int chunkSize = 4096) {

            SortedChunkedMap<K, V> series;
            var comparer = KeyComparer.GetDefault<K>() as IKeyComparer<K>;
            if (comparer != null) {

                //// mapId, current map version => map with chunk keys and versions
                //_remoteKeysLoader = 
                Func<long, long, Task<SortedMap<long, int>>> remoteKeysLoader = (mapid, version) => {

                    var key = "keys:" + mapid.ToString();
                    var cached = _cache.Get(key);
                    if (cached != null) {
                        return Task.FromResult(cached as SortedMap<long, int>);
                    }

                    var sm = new SortedMap<long, int>();
                    var chunks = _connection.Query<SeriesChunk>("SELECT ChunkKey, Version from " + ChunkTableName + "" +
                                                         " WHERE Id = @Id ORDER BY ChunkKey",
                        new { Id = mapid });
                    foreach (var ch in chunks) {
                        sm.AddLast(ch.ChunkKey, ch.Version);
                    }

                    _cache.Set(key, sm, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(5) });
                    return Task.FromResult(sm);
                };


                //// mapId, chunkKey => deserialied chunk
                Func<long, long, Task<SortedMap<K, V>>> remoteLoader = async (mapid, chKey) => {
                    var key = "chunk:" + mapid.ToString() + ":" + chKey.ToString();
                    var cached = _cache.Get(key);
                    if (cached != null) {
                        return cached as SortedMap<K, V>;
                    }

                    var value = Serializer.Deserialize<SortedMap<K, V>>(await _remoteChunkLoader(mapid, chKey));
                    //Debug.Assert(sod.Version == value.Version, "Serialized ChunkValue Version doesn't equal to TimeSeriesChunk Version");
                    //Debug.Assert(sod.Count == value.Count, "Serialized ChunkValue Count doesn't equal to TimeSeriesChunk Count");
                    _cache.Set(key, value, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(5) });
                    return value;
                };


                //// mapId, chunkKey, deserialied chunk => whole map version
                Func<long, long, SortedMap<K, V>, Task<long>> remoteSaver = (mapid, chKey, value) => {

                    var cacheKey = "chunk:" + mapid.ToString() + ":" + chKey.ToString();
                    _cache.Set(cacheKey, value, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(5) });

                    var setSQL = @"INSERT INTO " + ChunkTableName +
                                 " (Id,ChunkKey,ChunkValue,Count,Version)" +
                                 " VALUES ( @id, @dt, @ch, @count, @version) ON DUPLICATE KEY UPDATE ChunkValue=@ch, Count=@count, Version = @version";
                    var processedTmp = _connection.Execute(setSQL,
                        new {
                            id = mapid,
                            dt = chKey,
                            ch = Serializer.Serialize(value),
                            count = value.Count,
                            version = value.Version
                        });
                    if (processedTmp < 1) throw new ApplicationException("Cannot set value");

                    return Task.FromResult(0L);
                    //}
                };

                // mapId, chunkKey, direction => whole map version
                Func<long, long, Lookup, Task<long>> remoteRemover = (mapId, key, direction) => {
                    var r2 = false;
                    string setSQL;
                    int processedTmp;
                    switch (direction) {
                        case Lookup.EQ:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" +
                                            " WHERE Id = @id AND ChunkKey = @dt LIMIT 1";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            if (processedTmp == 0) {
                                r2 = false;
                            } else if (processedTmp > 1) {
                                throw new ApplicationException("Deleted more than one row");
                            } else {
                                r2 = true;
                            }

                            break;
                        case Lookup.LT:

                            setSQL = @"DELETE FROM " + ChunkTableName + "" +
                                            " WHERE Id = @id AND ChunkKey < @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;

                            break;
                        case Lookup.LE:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" +
                                            " WHERE Id = @id AND ChunkKey <= @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;

                            break;
                        case Lookup.GE:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" +
                                            " WHERE Id = @id AND ChunkKey >= @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;

                            break;
                        case Lookup.GT:
                            setSQL = @"DELETE FROM " + ChunkTableName + "" +
                                            " WHERE Id = @id AND ChunkKey > @dt ";
                            processedTmp = _connection.Execute(setSQL, new { id = mapId, dt = key });
                            r2 = processedTmp > 0;
                            break;
                        default:
                            break;
                    }
                    // RemoteSeries treats positive as true
                    return r2 ? Task.FromResult(1L) : Task.FromResult(-1L);
                };

                // mapId, chunkKey => lock releaser
                Func<long, long, Task<IDisposable>> remoteLocker = (g, v) => Task.FromResult(new DummyDisposable() as IDisposable);

                //Int64 localVersion,
                //// chunk key -> chunk version
                IOrderedMap<long, int> localKeysCache = new SortedMap<long, int>();

                Func<long, long, SortedMap<K, V>> localChunksCacheGet = (guid, chKey) => {
                    var cacheKey = guid + ":" + chKey;
                    var cached = _cache.Get(cacheKey) as SortedMap<K, V>;
                    return cached;
                };

                Action<long, long, SortedMap<K, V>> localChunksCacheSet = (guid, chKey, value) => {
                    var cacheKey = guid + ":" + chKey;
                    _cache.Set(cacheKey, value, new CacheItemPolicy() { SlidingExpiration = new TimeSpan(0, 15, 0) });
                };

                //// chunk key -> deserialized chunk version
                IOrderedMap<long, SortedMap<DateTime, V>> localChunksCache = new SortedMap<long, SortedMap<DateTime, V>>();


                Func<IComparer<K>, IOrderedMap<K, SortedMap<K, V>>> outerFactory = (cmp) => {
                    return new RemoteChunksSeries<K, V, long>(seriesId, cmp as IKeyComparer<K>, remoteKeysLoader, remoteLoader,
                        remoteSaver,
                        remoteRemover, remoteLocker, 0, localKeysCache, localChunksCacheGet, localChunksCacheSet);
                };

                series = new SortedChunkedMap<K, V>(outerFactory, comparer, chunkSize);
                // better be safe
                series.IsSynchronized = true;
                return series;

            } else {
                throw new NotSupportedException("Only type that have IKeyComparer<K> are supported");
            }
            throw new NotImplementedException();
        }
    }
}
