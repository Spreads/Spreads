using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads
{
    public interface IPublisher
    {
    }

    public interface ISubscriber<in T> : IObserver<T> {
        void OnSubscribe(ISubscription s);
    }

    public interface ISubscription {
        /// <summary>
        /// No events will be sent by a {@link Publisher} until demand is signaled via this method.
        /// 
        /// It can be called however often and whenever needed — but the outstanding cumulative demand must never exceed long.MaxValue.
        /// An outstanding cumulative demand of long.MaxValue may be treated by the Publisher as "effectively unbounded".
        /// 
        /// Whatever has been requested can be sent by the Publisher so only signal demand for what can be safely handled.
        /// 
        /// A Publisher can send less than is requested if the stream ends but then must emit either Subscriber.OnError(Throwable)}
        /// or Subscriber.OnCompleted().
        /// </summary>
        /// <param name="n">the strictly positive number of elements to requests to the upstream Publisher</param>
        void Request(long n);
        
        /// <summary>
        /// Request the Publisher to stop sending data and clean up resources.
        /// Data may still be sent to meet previously signalled demand after calling cancel as this request is asynchronous.
        /// </summary>
        void Cancel();
    }

    public interface IProcessor {

    }
}
