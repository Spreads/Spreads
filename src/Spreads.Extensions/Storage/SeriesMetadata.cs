using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {

    public class SeriesMetadata {
        public long Id { get; set; }
        public string TextId { get; set; }
        public UUID Uuid { get; set; }

        public int KeyTypeCode { get; set; }
        public int KeyTypeSize { get; set; }
        public string KeyTypeName { get; set; }
        public string KeyTypeSchema { get; set; }

        public int ValueTypeCode { get; set; }
        public int ValueTypeSize { get; set; }
        public string ValueTypeName { get; set; }
        public string ValueTypeSchema { get; set; }
    }
}
