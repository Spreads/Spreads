// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.Data.Sqlite;
using Spreads.Buffers;
using System;
using System.Data.Common;

namespace Spreads.Storage {

    public class SQLiteStorageProvider : StorageProvider {
        private const string PanelDefinitionTable = "spreads_panel_definition";
        private const string PanelChunksTable = "spreads_panel_chunks";
        private const string PanelStateTable = "spreads_panel_state";

        private readonly SqliteConnection _connection;
        private readonly string _tablePrefix;

        public SQLiteStorageProvider(SqliteConnection connection, string tablePrefix = "") {
            _connection = connection;
            _tablePrefix = tablePrefix;
            if (_connection.State != System.Data.ConnectionState.Open) {
                throw new ArgumentException("Connection must be open");
            }

            CreateTables();
        }

        public virtual void CreateTables() {
            var createSeriesChunksTable =
                $"CREATE TABLE IF NOT EXISTS `{_tablePrefix + PanelChunksTable}` (\n" +
                "  `PanelId` INTEGER NOT NULL,\n" +
                "  `ColumnId` INTEGER NOT NULL,\n" +
                "  `ChunkKey` INTEGER NOT NULL,\n" +
                "  `LastKey` INTEGER,\n" +
                "  `Version` INTEGER NOT NULL,\n" +
                "  `Count` INTEGER NOT NULL,\n" +
                "  `Keys` BLOB,\n" +
                "  `Values` BLOB,\n" +
                "  PRIMARY KEY (`PanelId`,`ChunkKey`,`ColumnId`)\n)"; // NB ChunkKey before ColumnId

            var command = _connection.CreateCommand();
            command.CommandText = createSeriesChunksTable;
            command.ExecuteNonQuery();
            command.Dispose();
        }


        #region Add

        public virtual DbCommand SetCommand() {
            //var sql = $@"INSERT OR REPLACE INTO {_tablePrefix + PanelChunksTable} (Id,ChunkKey,Count,Version,ChunkValue)" + " VALUES ( @id, @chKey, @count, @version, @chVal); UPDATE " + IdTableName + " SET Version = @version WHERE Id = @id;";
            throw new NotImplementedException();
        }

        public virtual DbCommand AddCommand() {
            //var sql = $@"INSERT INTO {_tablePrefix + PanelChunksTable} (Id,ChunkKey,Count,Version,ChunkValue)" + " VALUES ( @id, @chKey, @count, @version, @chVal); UPDATE " + IdTableName + " SET Version = @version WHERE Id = @id;";
            throw new NotImplementedException();
        }

        public sealed override bool Add(RawPanelChunk rawPanelChunk, bool replace = false) {
            var cmd = replace ? SetCommand() : AddCommand();
            using (var transaction = _connection.BeginTransaction()) {
                cmd.Transaction = transaction;
                try {
                    // insert prime
                    SetDbCommandParametersForAddSet(cmd, rawPanelChunk.Prime);
                    if (cmd.ExecuteNonQuery() != 1) {
                        throw new InvalidProgramException();
                    }
                    // insert columns
                    for (int i = 0; i < rawPanelChunk.ColumnCount; i++) {
                        var column = rawPanelChunk[i];
                        SetDbCommandParametersForAddSet(cmd, column);
                        if (cmd.ExecuteNonQuery() != 1) {
                            throw new InvalidProgramException();
                        }
                    }
                    transaction.Commit();
                    return true;
                } catch (Exception) {
                    transaction.Rollback();
                    return false;
                }
            }
        }


        private static void SetDbCommandParametersForAddSet(DbCommand cmd, RawColumnChunk column) {
            //public struct RawColumnChunk {
            //    public readonly int PanelId;
            //    public readonly int ColumnId;
            //    public readonly long ChunkKey;
            //    public readonly long Version;
            //    public readonly int Count;
            //    public readonly PreservedMemory<byte> Keys;
            //    public readonly PreservedMemory<byte> Values;
            //}
            cmd.Parameters["@PanelId"].Value = column.PanelId;
            cmd.Parameters["@ColumnId"].Value = column.ColumnId;
            cmd.Parameters["@ChunkKey"].Value = column.ChunkKey;
            cmd.Parameters["@Version"].Value = column.Version;
            cmd.Parameters["@Count"].Value = column.Count;
            SetReservedMemoryToDbParameter(cmd.Parameters["@Keys"], column.Keys);
            SetReservedMemoryToDbParameter(cmd.Parameters["@Values"], column.Values);
        }

        private static void SetReservedMemoryToDbParameter(DbParameter parameter, PreservedMemory<byte> memory) {
            ArraySegment<byte> segment;
            if (!memory.Memory.TryGetArray(out segment)) {
                throw new NotSupportedException("Currently only arrays-backed OwnedMemory is supported");
            }

            var sqliteParam = parameter as SqliteParameter;
            if (sqliteParam != null) {
                // we have offset property in the  SqliteParameter
                sqliteParam.Offset = segment.Offset;
                sqliteParam.Size = segment.Count;
                sqliteParam.Value = segment.Array;
                return;
            }
            if (segment.Offset != 0) {
                throw new NotImplementedException("TODO copy to another pooled array with offset 0.");
            }
            parameter.Size = segment.Count;
            parameter.Value = segment.Array;
        }

        #endregion


        public override bool Remove(int panelId, long key, Lookup direction) {
            throw new NotImplementedException();
        }


        #region Get
        ... // TODO continue here
        public virtual DbCommand GetEqCommand() {
            var sql = $"SELECT * FROM {_tablePrefix + PanelChunksTable} " +
                      "WHERE `PanelId` = @PanelId AND `ChunkKey` = @ChunkKey " +
                      "ORDER BY `ColumnId` ASC; ";
            throw new NotImplementedException();
        }

        public virtual DbCommand GetLtCommand() {
            // for le, replace < with <=
            // for ge, gt replace DESC with ASC
            // last order by is alway the same, for le/lt we should do reverse traversal in code
            var sql = $"SELECT * FROM {_tablePrefix + PanelChunksTable} " +
                      " WHERE `PanelId` = @PanelId AND `ChunkKey` IN " +
                      $"(SELECT DISTINCT `ChunkKey` FROM {_tablePrefix + PanelChunksTable} WHERE " +
                      $"`ChunkKey` < @ChunkKey ORDER BY `ChunkKey` DESC LIMIT @Limit ) " +
                      "ORDER BY `ChunkKey`, `ColumnId` ASC; ";
            throw new NotImplementedException();
        }

        public sealed override int TryGetChunksAt(int panelId, long key, Lookup direction,
            ref RawPanelChunk[] rawPanelChunks, int[] columnIds = null) {
            foreach (var rawPanelChunk in rawPanelChunks) {
                if (rawPanelChunk != null) throw new ApplicationException("THe rawPanelChunks[] array must be cleared");
            }
            if (rawPanelChunks == null) throw new ArgumentNullException(nameof(rawPanelChunks));
            if (columnIds != null) throw new NotImplementedException("TODO subset of columns");
            var limit = rawPanelChunks.Length;
            // if limit > 1, need a subquery that will get all the keys and 
            DbCommand command = null;
            var reader = command.ExecuteReader();
            ProcessDbReader(reader, ref rawPanelChunks);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Fill the rawPanelChunks array with values from Db
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="rawPanelChunks"></param>
        private static void ProcessDbReader(DbDataReader reader, ref RawPanelChunk[] rawPanelChunks) {

            var rowCount = 0;
            int panelId;
            long chunkKey;
            while (reader.Read()) {
                var columnChunk = FillRawColumnChunk(reader);
                if (rowCount == 0) {
                    panelId = columnChunk.PanelId;
                    chunkKey = columnChunk.ChunkKey;
                }
                bool primeDone = false;
                if (!primeDone) {
                    var prime = columnChunk;
                    if (prime.ColumnId != 0) throw new ApplicationException("Wrong implementation of TryGetChunksAt SQL");
                    panelId = prime.PanelId;
                    chunkKey = prime.ChunkKey;
                    var columnId = reader.GetInt32(1);
                    ...
                    // TODO continue here
                } else {
                }
                rowCount++;
            }
        }

        /// <summary>
        /// Fills a new RawColumnChunk with values from the current DbDataReader position
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static RawColumnChunk FillRawColumnChunk(DbDataReader reader) {
            var panelId = reader.GetInt32(0);
            var columnId = reader.GetInt32(1);
            var chunkKey = reader.GetInt64(2);
            var lastKey = reader.GetInt64(3);
            var version = reader.GetInt64(4);
            var count = reader.GetInt32(5);

            var keyLength = reader.GetBytes(6, 0, null, 0, 0);
            var keys = BufferPool.PreserveMemory(checked((int)keyLength));
            ArraySegment<byte> keysSegment;
            if (!keys.Memory.TryGetArray(out keysSegment)) {
                throw new NotSupportedException("Currently only arrays-backed OwnedMemory is supported");
            }
            reader.GetBytes(6, 0, keysSegment.Array, keysSegment.Offset, keysSegment.Count);

            var valuesLength = reader.GetBytes(7, 0, null, 0, 0);
            var values = BufferPool.PreserveMemory(checked((int)valuesLength));
            ArraySegment<byte> valuesSegment;
            if (!values.Memory.TryGetArray(out valuesSegment)) {
                throw new NotSupportedException("Currently only arrays-backed OwnedMemory is supported");
            }
            reader.GetBytes(7, 0, valuesSegment.Array, valuesSegment.Offset, valuesSegment.Count);

            var columnChunk = new RawColumnChunk(panelId, columnId, chunkKey, lastKey, version, count, keys, values);

            return columnChunk;
        }

        #endregion

        public override ValueTuple<long, long> GetRange(int panelId) {
            throw new NotImplementedException();
        }
    }
}