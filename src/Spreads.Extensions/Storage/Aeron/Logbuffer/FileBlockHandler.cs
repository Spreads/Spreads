using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage.Aeron.Logbuffer {
    public delegate void FileBlockHandler(FileStream fileChannel, long offset, int length, int sessionId, int termId);

}
