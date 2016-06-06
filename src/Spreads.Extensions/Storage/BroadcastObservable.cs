using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Spreads.Serialization;
using Spreads.Storage.Aeron.Logbuffer;

namespace Spreads.Storage {


    public class BroadcastObservable<T> : IObservable<T>, IObserver<T>, DataRepository.IAcceptCommand {
        private readonly AppendLog _appendLog;
        private readonly string _channelId;
        private readonly int _pid;

        List<IObserver<T>> _observers;
        private readonly UUID _uuid;

        internal BroadcastObservable(AppendLog appendLog, string channelId, int pid) {
            _appendLog = appendLog;
            _channelId = channelId;
            _pid = pid;
            _uuid = new UUID(channelId);
            _observers = new List<IObserver<T>>();
        }

        internal UUID UUID => _uuid;
        public string ChannelId => _channelId;

        public IDisposable Subscribe(IObserver<T> observer) {
            if (!_observers.Contains(observer)) _observers.Add(observer);
            return new Unsubscriber(_observers, observer);
        }

        // these methods are used to broadcast messages from this instance to 
        // all other subscribers excluding this insatnce
        public unsafe void OnNext(T value) {
            BufferClaim claim;
            MemoryStream ms = null;

            var header = new MessageHeader {
                UUID = UUID,
                MessageType = MessageType.Broadcast,
            };

            var len = TypeHelper<T>.SizeOf(value, ref ms) + MessageHeader.Size;
            _appendLog.Claim(len, out claim);
            *(MessageHeader*)(claim.Data) = header;
            TypeHelper<T>.StructureToPtr(value, claim.Data + MessageHeader.Size, ms);
            claim.ReservedValue = _pid;
            claim.Commit();
        }

        public unsafe void OnError(Exception error) {
            BufferClaim claim;
            var header = new MessageHeader {
                UUID = UUID,
                MessageType = MessageType.Error,
            };
            _appendLog.Claim(MessageHeader.Size, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = _pid;
            claim.Commit();
        }

        public unsafe void OnCompleted() {
            BufferClaim claim;
            var header = new MessageHeader {
                UUID = UUID,
                MessageType = MessageType.Complete,
            };
            _appendLog.Claim(MessageHeader.Size, out claim);
            *(MessageHeader*)(claim.Data) = header;
            claim.ReservedValue = _pid;
            claim.Commit();
        }

        public void Dispose() {
            // TODO
        }

        public unsafe void ApplyCommand(DirectBuffer buffer) {
            var dataStart = buffer.Data;
            var header = *(MessageHeader*)(dataStart);
            if (header.MessageType != MessageType.Broadcast
                || header.MessageType != MessageType.Complete
                || header.MessageType != MessageType.Error) throw new InvalidOperationException("Wrong command type");


            Trace.Assert(header.UUID == UUID);
            var type = header.MessageType;
            switch (type) {
                case MessageType.Complete:
                    foreach (var observer in _observers)
                        observer.OnCompleted();
                    break;
                case MessageType.Error:
                    foreach (var observer in _observers)
                        observer.OnError(new NotImplementedException("TODO error broadcast is not implemented"));
                    break;
                case MessageType.Broadcast:
                    foreach (var observer in _observers)
                        observer.OnNext(TypeHelper<T>.PtrToStructure(dataStart + MessageHeader.Size));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private class Unsubscriber : IDisposable {
            private List<IObserver<T>> _observers;
            private IObserver<T> _observer;

            public Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer) {
                this._observers = observers;
                this._observer = observer;
            }

            public void Dispose() {
                if (!(_observer == null)) _observers.Remove(_observer);
            }
        }
    }


    // one per machine, manages state
    public class Conductor {
    }

    public class VirtualActor {
        public string Name { get; set; }
        public string Id { get; set; }
        public string RequestChannel { get; set; }
        public string ResponseChannel { get; set; }
    }
}
