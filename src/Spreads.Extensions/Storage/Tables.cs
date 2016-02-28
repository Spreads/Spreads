namespace Spreads.Storage
{

    public class SeriesId {
        public long Id { get; set; }
        public string TextId { get; set; }
        // the easiest way to avoid collisions by string TextId name is to store type names together with series id, e.g. my_series<>
        public string KeyType { get; set; }
        public string ValueType { get; set; }
    }



    public class SeriesChunk {
        public long Id { get; set; }
        public long ChunkKey { get; set; }
        public int Count { get; set; }
        public int Version { get; set; }
        public byte[] ChunkValue { get; set; }
    }
}