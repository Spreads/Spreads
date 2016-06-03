using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage {


    public class BroadcastObservable<T> : IObservable<T>, IObserver<T> {

        public void Broadcast(T value)
        {
            
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            throw new NotImplementedException();
        }


        // these methods are used to broadcast messages from this instance to 
        // all other subscribers excluding this insatnce
        public void OnNext(T value)
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }
    }


    // one per machine, manages state
    public class Conductor
    {
        
    }

    public class VirtualActor
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string RequestChannel { get; set; }
        public string ResponseChannel { get; set; }

    }
}
