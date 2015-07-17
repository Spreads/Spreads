using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads {

    // TODO move to SpreadsDB project
    public interface IPersistentOrderedMap<K,V> : IOrderedMap<K, V>
    {
        // TODO WriteAck with get, set (or only get, this depends on implementation)

        /// <summary>
        /// Persist all unsaved changes 
        /// </summary>
        void Flush();
    }
}
