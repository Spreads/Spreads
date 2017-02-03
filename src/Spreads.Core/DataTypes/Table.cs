// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Spreads.Serialization;

namespace Spreads.DataTypes {

    [JsonConverter(typeof(TableJsonConverter))]
    public class Table : Matrix<Variant>, IEquatable<Table> {

        // TODO
        internal static int LinearSearchLimit = 30;

        [JsonIgnore]
        private Dictionary<object, int> _rowKeyIndex;

        [JsonIgnore]
        private Dictionary<object, int> _columnKeyIndex;

        private readonly int _rows;
        private readonly int _columns;

        public Table(int rows, int columns) : this(new Variant[rows, columns]) {
        }

        public Table(Variant[,] data) : base(data) {
            _rows = this.RowsCount;
            _columns = this.ColumnsCount;
        }

        public string TableName { get; set; }
        public DateTime DateTime { get; set; }
        public long Version { get; set; }

        public Variant this[int row, int column]
        {
            get
            {
                return Data[row, column];
            }
            set
            {
                Data[row, column] = value;
            }
        }

        public object this[object rowKey, object columnKey]
        {
            get
            {
                var row = GetRowIndex(Variant.FromObject(rowKey));
                if (row == -1) return null;
                var column = GetColumnIndex(Variant.FromObject(columnKey));
                if (column == -1) return null;
                return Variant.ToObject(Data[row, column]);
            }
        }

        public object this[Variant rowKey, Variant columnKey]
        {
            get
            {
                var row = GetRowIndex(rowKey);
                if (row == -1) return null;
                var column = GetColumnIndex(columnKey);
                if (column == -1) return null;
                return Variant.ToObject(Data[row, column]);
            }
        }

        private int GetRowIndex(Variant rowKey) {
            if (_rows < LinearSearchLimit) {
                for (var i = 0; i < _rows; i++) {
                    if (Data[i, 0] == rowKey) {
                        return i;
                    }
                }
                return -1;
            }

            if (_rowKeyIndex == null) {
                PopulateRowKeys();
            }

            int row;
            Debug.Assert(_rowKeyIndex != null, "_rowKeyIndex != null");
            if (!_rowKeyIndex.TryGetValue(rowKey, out row)) {
                return -1;
            }
            return row;
        }

        private int GetColumnIndex(Variant columnKey) {
            if (_columns < LinearSearchLimit) {
                for (var i = 0; i < _rows; i++) {
                    if (Data[0, i] == columnKey) {
                        return i;
                    }
                }
                return -1;
            }

            if (_columnKeyIndex == null) {
                PopulateColumnKeys();
            }

            int column;
            Debug.Assert(_columnKeyIndex != null, "_columnKeyIndex != null");
            if (!_columnKeyIndex.TryGetValue(columnKey, out column)) {
                return -1;
            }
            return column;
        }

        private void PopulateRowKeys() {
            _rowKeyIndex = new Dictionary<object, int>();
            for (int i = 0; i < RowsCount; i++) {
                var value = Variant.ToObject(Data[i, 0]);
                if (!_rowKeyIndex.ContainsKey(value)) {
                    _rowKeyIndex[value] = i;
                }
            }
        }

        private void PopulateColumnKeys() {
            _columnKeyIndex = new Dictionary<object, int>();
            for (int i = 0; i < ColumnsCount; i++) {
                var value = Variant.ToObject(Data[0, i]);
                if (!_columnKeyIndex.ContainsKey(value)) {
                    _columnKeyIndex[value] = i;
                }
            }
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            if (obj == null) {
                return false;
            }
            var t = obj as Table;
            return this.Equals(t);
        }

        public override int GetHashCode() {
            return Data.GetHashCode();
        }

        public bool Equals(Table t) {
            if (t == null) return false;
            if (this.RowsCount != t.RowsCount || this.ColumnsCount != t.ColumnsCount) return false;
            for (var r = 0; r < RowsCount; r++) {
                for (var c = 0; c < ColumnsCount; c++) {
                    if (this[r, c] != t[r, c]) {
                        return false;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// Create a full snapshort of this table
        /// </summary>
        /// <returns></returns>
        public TableDto ToSnapshot() {
            var dto = new TableDto {
                TableName = this.TableName,
                Id = this.TableName,
                DateTime = this.DateTime,
                RowsCount = Data.GetLength(0),
                ColumnsCount = Data.GetLength(1),
                IsComplete = true,
                Version = Version,
                Cells = new List<TableCell>()
            };
            for (int row = 0; row < dto.RowsCount; row++) {
                for (int column = 0; column < dto.ColumnsCount; column++) {
                    var value = Data[row, column];
                    // default(Variant) has this type
                    if (value.TypeEnum == TypeEnum.None) continue;
                    dto.Cells.Add(new TableCell {
                        Row = row,
                        Column = column,
                        Value = value
                    });
                }
            }

            return dto;
        }

        public TableDto ToDelta(Table previous) {
            if (RowsCount != previous.RowsCount || ColumnsCount != previous.ColumnsCount) {
                return ToSnapshot();
            }
            var dto = new TableDto {
                TableName = this.TableName,
                Id = this.TableName,
                DateTime = this.DateTime,
                RowsCount = Data.GetLength(0),
                ColumnsCount = Data.GetLength(1),
                IsComplete = false,
                Version = Version,
                Cells = new List<TableCell>()
            };
            for (var row = 0; row < dto.RowsCount; row++) {
                for (var column = 0; column < dto.ColumnsCount; column++) {
                    var value = Data[row, column];
                    var previousValue = previous.Data[row, column];
                    if (value == previousValue) {
                        continue;
                    }

                    dto.Cells.Add(new TableCell {
                        Row = row,
                        Column = column,
                        Value = value
                    });
                }
            }

            return dto;
        }

        public static Table FromSnapshot(TableDto dto) {
            if (!dto.IsComplete) throw new ArgumentException("Snapshot must be complete");
            var table = new Table(dto.RowsCount, dto.ColumnsCount) {
                TableName = dto.TableName,
                DateTime = dto.DateTime,
                Version = dto.Version,
            };
            foreach (var cell in dto.Cells) {
                table.Data[cell.Row, cell.Column] = cell.Value;
            }

            return table;
        }

        public static Table ApplyDelta(Table previous, TableDto dto) {
            if (previous.RowsCount != dto.RowsCount || previous.ColumnsCount != dto.ColumnsCount || dto.IsComplete) {
                Trace.Assert(dto.IsComplete);
                return FromSnapshot(dto);
            }
            var dataCopy = (Variant[,])previous.Data.Clone();
            var table = new Table(dataCopy) {
                TableName = dto.TableName,
                DateTime = dto.DateTime,
                Version = dto.Version,
            };
            foreach (var cell in dto.Cells) {
                table.Data[cell.Row, cell.Column] = cell.Value;
            }

            return table;
        }
    }

    [JsonConverter(typeof(TableCellJsonConverter))]
    public struct TableCell {

        //[JsonProperty("r")]
        public int Row;

        //[JsonProperty("c")]
        public int Column;

        //[JsonProperty("v")]
        public Variant Value;
    }

    [MessageType("table_delta")]
    public class TableDto : IMessage {

        [JsonProperty("type")]
        public string Type => "table_delta";

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version")]
        public long Version { get; set; }

        /// <summary>
        /// True is this is a full snapshot, false if it is a delta
        /// </summary>
        [JsonProperty("is_complete")]
        public bool IsComplete { get; set; }

        [JsonProperty("table_name")]
        public string TableName { get; set; }

        [JsonProperty("datetime")]
        public DateTime DateTime { get; set; }

        [JsonProperty("cells")]
        public List<TableCell> Cells { get; set; }

        [JsonProperty("rows")]
        public int RowsCount { get; set; }

        [JsonProperty("columns")]
        public int ColumnsCount { get; set; }
    }

    public class TableCellJsonConverter : JsonConverter {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var cell = (TableCell)value;
            writer.WriteStartArray();
            writer.WriteValue(cell.Row);
            writer.WriteValue(cell.Column);
            serializer.Serialize(writer, cell.Value);
            //writer.WriteValue(cell.Value);
            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.Null) {
                return Variant.FromObject(null);
            }
            if (reader.TokenType != JsonToken.StartArray) {
                throw new Exception("Invalid JSON for Variant type");
            }

            var row = reader.ReadAsInt32();
            var column = reader.ReadAsInt32();

            Variant obj;
            if (!reader.Read()) throw new Exception("Cannot read JSON");
            obj = serializer.Deserialize<Variant>(reader);

            if (!reader.Read()) throw new Exception("Cannot read JSON");
            Trace.Assert(reader.TokenType == JsonToken.EndArray);
            Debug.Assert(row != null, "row != null");
            Debug.Assert(column != null, "column != null");
            return new TableCell() {
                Row = row.Value,
                Column = column.Value,
                Value = obj
            };
        }

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(TableCell);
        }
    }


    public class TableJsonConverter : JsonConverter {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var table = (Table)value;
            var tableDto = table.ToSnapshot();
            serializer.Serialize(writer, tableDto);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            var tableDto = serializer.Deserialize<TableDto>(reader);
            var table = Table.FromSnapshot(tableDto);
            return table;
        }

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(TableCell);
        }
    }
}
