using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {
    public class BroadcastObservable<T> : IObservable<T> {

        public void Broadcast(T value)
        {
            
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            throw new NotImplementedException();
        }
    }
}
