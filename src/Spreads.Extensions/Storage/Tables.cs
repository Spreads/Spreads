using System.Diagnostics;

namespace Spreads.Storage {

    public class SeriesId {

        public SeriesId() {}

        public SeriesId(string extendedSeriesId) {
            var parts = extendedSeriesId.Split('|');
            Trace.Assert(parts.Length == 3);
            TextId = parts[0];
            KeyType = parts[1];
            ValueType = parts[2];
        }
        public long Id { get; set; }
        public string TextId { get; set; }
        public byte[] UUID { get; set; }
        // the easiest way to avoid collisions by string TextId name is to store type names together with series id, e.g. my_series<>
        public string KeyType { get; set; }
        public string ValueType { get; set; }

        public override string ToString() {
            return TextId + "|" + KeyType + "|" + ValueType;
        }

        public static implicit operator SeriesId(string extendedSeriesId) {
            return new SeriesId(extendedSeriesId);
        }
    }


    public class SeriesChunk {
        /// <summary>
        /// Series id
        /// </summary>
        public long Id { get; set; }
        public long ChunkKey { get; set; }
        public long Count { get; set; }
        public long Version { get; set; }
        public byte[] ChunkValue { get; set; }
    }


}