using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Serialization;

namespace Spreads.Storage.Aeron.Logbuffer {
    /// <summary>
    /// Function for handling a block of message fragments scanned from the log.
    /// </summary>
    public delegate void BlockHandler(DirectBuffer buffer, int offset, int length, int sessionId, int termId);

}
