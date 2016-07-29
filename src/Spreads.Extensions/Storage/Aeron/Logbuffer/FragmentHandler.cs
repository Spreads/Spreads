using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Storage.Aeron.Logbuffer {
    public delegate void FragmentHandler(DirectBuffer buffer, int offset, int length, Header header);

}
