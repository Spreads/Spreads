using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads.Buffers;
using Spreads.Serialization;

namespace Spreads.Storage.Aeron.Logbuffer {
    public delegate ControlledFragmentHandlerAction ControlledFragmentHandler(DirectBuffer buffer, 
        int offset, int length, Header header);

    public enum ControlledFragmentHandlerAction {
        /// <summary>
        /// Abort the current polling operation and do not advance the position for this fragment.
        /// </summary>
        ABORT,

        /// <summary>
        /// Break from the current polling operation and commit the position as of the end of the current fragment
        /// being handled.
        /// </summary>
        BREAK,

        /// <summary>
        /// Continue processing but commit the position as of the end of the current fragment so that
        /// flow control is applied to this point.
        /// </summary>
        COMMIT,

        /// <summary>
        /// Continue processing taking the same approach as the in
        /// <seealso cref="FragmentHandler#onFragment(DirectBuffer, int, int, Header)"/>.
        /// </summary>
        CONTINUE,
    }

}
