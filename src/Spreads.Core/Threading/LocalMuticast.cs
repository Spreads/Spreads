// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spreads.Threading
{
    // what if we could use local UDP multicast as notification channel?
    public class LocalMulticast<TMessage> : IDisposable where TMessage : struct
    {
        private readonly Action<TMessage> _handler;
        private readonly int _bufferLength;

        internal long ReceiveCounter;
        internal long SendCounter;

        private readonly Socket _socket;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly CachedEndPoint _inEndPoint;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly CachedEndPoint _mcEndPoint;
        private readonly ObjectPool<SocketAsyncEventArgs> _sendArgsPool;
        private readonly string _name;

        public LocalMulticast(int port, Action<TMessage> handler = null, string name = null)
        {
            _handler = handler;
            _name = name ?? String.Empty;

            var msgSize = TypeHelper<TMessage>.FixedSize;
            if (msgSize <= 0)
            {
                throw new ArgumentException($"Type {typeof(TMessage).Name} must be a blittable struct");
            }

            _bufferLength = msgSize;

            _sendArgsPool = new ObjectPool<SocketAsyncEventArgs>(SendArgsFactory, Environment.ProcessorCount * 4);

            var multicastAddress = IPAddress.Parse("239.199.99.9");
            _mcEndPoint = new CachedEndPoint(new IPEndPoint(multicastAddress, port));
            _inEndPoint = new CachedEndPoint(new IPEndPoint(IPAddress.Loopback, port));

            _socket = new Socket(_inEndPoint.AddressFamily,
                SocketType.Dgram, ProtocolType.Udp);

            _socket.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);

            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.MulticastTimeToLive,
                2);

            _socket.ExclusiveAddressUse = false;
            _socket.Blocking = false;
            _socket.EnableBroadcast = true;
            // Maybe a fluke, bit perf drops and definitely not improves with this: _socket.UseOnlyOverlappedIO = true;

            _socket.Bind(_inEndPoint);

            // If you are using a connectionless protocol such as UDP,
            // you do not have to call Connect before sending and
            // receiving data. You can use SendTo and ReceiveFrom to
            // synchronously communicate with a remote host.
            // If you do call Connect, any datagrams that arrive from
            // an address other than the specified default will be discarded.
            _socket.Connect(_inEndPoint);

            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.MulticastLoopback,
                true);

            // join on loopback interface
            var mcastOption = new MulticastOption(multicastAddress, NetworkInterface.LoopbackInterfaceIndex);
            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                mcastOption);

            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
#pragma warning disable 618
                (int)IPAddress.Loopback.Address);
#pragma warning restore 618
            // see https://github.com/dotnet/corefx/issues/25699 if there are issues on non-Windows
            // IPAddress.HostToNetworkOrder(NetworkInterface.LoopbackInterfaceIndex));

            // Another option to limit source to loopback
            //byte[] membershipAddresses = new byte[12]; // 3 IPs * 4 bytes (IPv4)
            //Buffer.BlockCopy(multicastAddress.GetAddressBytes(), 0, membershipAddresses, 0, 4);
            //Buffer.BlockCopy(IPAddress.Loopback.GetAddressBytes(), 0, membershipAddresses, 4, 4);
            //Buffer.BlockCopy(IPAddress.Loopback.GetAddressBytes(), 0, membershipAddresses, 8, 4);
            //_socket.SetSocketOption(SocketOptionLevel.IP,
            //    SocketOptionName.AddSourceMembership,
            //    membershipAddresses);

            StartReceive();
        }

        /// <summary>
        /// Instance name useful for debugging.
        /// </summary>
        public string Name => _name ?? String.Empty;

        /// <summary>
        /// Start additional receiver. A single receiver is started from the constructor.
        /// </summary>
        public void StartReceive()
        {
            SocketAsyncEventArgs args = ReceiveArgsFactory(); // _receiveArgsPool.Allocate();

            if (!_socket.ReceiveAsync(args))
            {
                ProcessReceive(args);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send(TMessage message)
        {
            var args = _sendArgsPool.Allocate();
            Unsafe.WriteUnaligned(ref args.Buffer[0], message);

            if (!_socket.SendToAsync(args))
            {
                ProcessSend(args);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref SendCounter);
            _sendArgsPool.Free(e);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            do
            {
                if (e.SocketError == SocketError.Success)
                {
                    var message = Unsafe.ReadUnaligned<TMessage>(ref e.Buffer[0]);
                    _handler?.Invoke(message);
                    Interlocked.Increment(ref ReceiveCounter);
                }
                else
                {
                    Trace.TraceWarning($"Error in LocalMuticast Receive {e.SocketError}");
                }
            } while (!_cts.IsCancellationRequested && !_socket.ReceiveAsync(e));
        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;

                case SocketAsyncOperation.Send:
                case SocketAsyncOperation.SendTo:
                    ProcessSend(e);
                    break;

                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SocketAsyncEventArgs SendArgsFactory()
        {
            var args = new SocketAsyncEventArgs();
            var buffer = new byte[_bufferLength];
            args.Completed += IO_Completed;
            args.SetBuffer(buffer, 0, _bufferLength);
            args.RemoteEndPoint = _mcEndPoint;
            args.SocketFlags = SocketFlags.Multicast;
            return args;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SocketAsyncEventArgs ReceiveArgsFactory()
        {
            var args = new SocketAsyncEventArgs();
            var buffer = new byte[_bufferLength];
            args.Completed += IO_Completed;
            args.SetBuffer(buffer, 0, _bufferLength);
            return args;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _socket?.Dispose();
        }

        /// <summary>
        /// Reduce GC by caching Create and Serialize methods.
        /// </summary>
        internal sealed class CachedEndPoint : EndPoint
        {
            private EndPoint _endPoint;
            private SocketAddress _serialized;

            private readonly IPEndPoint _ipep;

            public CachedEndPoint(IPEndPoint ipep)
            {
                _ipep = ipep;
            }

            public override AddressFamily AddressFamily => _ipep.AddressFamily;

            public override EndPoint Create(SocketAddress socketAddress)
            {
                return _endPoint ?? (_endPoint = _ipep.Create(socketAddress));
            }

            public override SocketAddress Serialize()
            {
                return _serialized ?? (_serialized = _ipep.Serialize());
            }
        }
    }
}
