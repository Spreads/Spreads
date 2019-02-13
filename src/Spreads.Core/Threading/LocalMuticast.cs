// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Collections.Concurrent;
using Spreads.Serialization;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Threading
{
    // what if we could use local UDP multicast as notification channel?
    public class LocalMulticast<TMessage> : IDisposable where TMessage : struct
    {
        private int _bufferLength;

        public long ReceiveCounter;
        public long SendCounter;

        private readonly Socket _socket;
        private readonly IPEndPoint _inEndPoint;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private IPAddress _multicastAddress;
        private IPEndPoint _mcEndPoint;
        private ObjectPool<SocketAsyncEventArgs> _argsPool;

        public LocalMulticast(int port)
        {
            var msgSize = TypeHelper<TMessage>.FixedSize;
            if (msgSize <= 0)
            {
                throw new ArgumentException($"Type {typeof(TMessage).Name} must be a blittable struct");
            }

            _bufferLength = msgSize;

            _argsPool = new ObjectPool<SocketAsyncEventArgs>(EventArgsFactory, Environment.ProcessorCount * 4);

            var nics = NetworkInterface
                .GetAllNetworkInterfaces();
            var adapter = nics
                .FirstOrDefault(x => x.NetworkInterfaceType == NetworkInterfaceType.Loopback);

            IPv4InterfaceProperties p = adapter.GetIPProperties().GetIPv4Properties();

            _multicastAddress = new IPAddress(0xFAFFFFEF);
            _mcEndPoint = new IPEndPoint(_multicastAddress, port);
            _inEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            _socket = new Socket(_inEndPoint.AddressFamily,
                SocketType.Dgram, ProtocolType.Udp);

            _socket.SetSocketOption(SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true);

            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.MulticastTimeToLive,
                1);

            // _socket.Blocking = true;

            // _socket.EnableBroadcast = true;
            _socket.Bind(_inEndPoint);
            _socket.Connect(_inEndPoint);

            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.MulticastLoopback,
                true);

            var mcastOption = new MulticastOption(_multicastAddress,
                NetworkInterface.LoopbackInterfaceIndex);
            _socket.SetSocketOption(SocketOptionLevel.IP,
                SocketOptionName.AddMembership,
                mcastOption);

            // _socket.ReceiveTimeout = 1000;
        }

        public bool IsReceiving => _receiveTask != null;

        public void StartReceive()
        {
            SocketAsyncEventArgs args = _argsPool.Allocate();

            if (!_socket.ReceiveAsync(args))
            {
                // if false, the Complete won't be called
                ProcessReceive(args);
                // OnReceiveCompleted(this, args);
            }

            Thread.Sleep(int.MaxValue - 1);
        }

        //public void StartReceive2()
        //{
        //    if (_receiveTask != null)
        //    {
        //        throw new InvalidOperationException("Already receiving");
        //    }
        //    _cts = new CancellationTokenSource();
        //    _receiveTask = Task.Run(async () =>
        //    {
        //        try
        //        {
        //            var buffer = new byte[4096];
        //            while (!_cts.IsCancellationRequested)
        //            {
        //                try
        //                {
        //                    // EndPoint mcep = _mcEndPoint;
        //                    var received = _socket.Receive(buffer);//, ref mcep);

        //                    //#if NETCOREAPP3_0
        //                    //                            var received = await _socket.ReceiveAsync(buffer, SocketFlags.Multicast, _cts.Token);
        //                    //#else
        //                    //                            var received = 0;
        //                    //                            throw new NotImplementedException();
        //                    //#endif
        //                    if (received > 0)
        //                    {
        //                        Interlocked.Increment(ref ReceiveCounter);
        //                        var segment = new ArraySegment<byte>(buffer, 0, received);
        //                        OnReceiveCompleted(segment);
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    Trace.TraceError(ex.Message + Environment.NewLine + ex);
        //                }
        //            }
        //        }
        //        finally
        //        {
        //            _receiveTask = null;
        //        }
        //    }, _cts.Token);
        //}

        public void StopReceive()
        {
            var t = _receiveTask;
            if (t == null)
            {
                Trace.TraceWarning("Calling StopReceive on non-receiving connection.");
                return;
            }
            _cts.Cancel();
            t.Wait();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send(TMessage message)
        {
            // _socket.Send(buffer, offset, count, SocketFlags.None);

            var args = _argsPool.Allocate();

            var buffer = args.Buffer;

            Unsafe.WriteUnaligned(ref buffer[0], message);

            // Array.Copy(buffer, 0, args.Buffer, 0, Math.Min(_bufferLength, buffer.Length));

            if (!_socket.SendAsync(args))
            {
                // if false, the Complete won't be called
                OnSendCompleted(this, args);
            }

            //#if NETCOREAPP3_0
            //            _socket.SendAsync(new ArraySegment<byte>(buffer, offset, count),
            //                SocketFlags.None);
            //#else
            //            throw new NotImplementedException();
            //#endif
            // Interlocked.Increment(ref SendCounter);
        }

        private void OnSendCompleted(object localMulticast, SocketAsyncEventArgs e)
        {
#if NET461
            if (!ExecutionContext.IsFlowSuppressed())
            {
                ExecutionContext.SuppressFlow();
            }
#endif
            Interlocked.Increment(ref SendCounter);

            _argsPool.Free(e);
        }

        public virtual void OnReceiveCompleted(TMessage message)
        {
            // Console.WriteLine("RCV: " + buffer.Count);
            // TODO write to AppendLog with a stream id supplied in ctor
            // Order restoration by sequence number and missed packets should be in a separate
            // process, which then writes data in-order into an AppendLog.
            // Some decoding could be done in that process as well since it is a shared cost
            // E.g. if a message could be represented by a blittable structure
            // or a sequence of such structs. Then reading of structs is just pointer deref.
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref SendCounter);
            _argsPool.Free(e);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            do
            {
                var message = Unsafe.ReadUnaligned<TMessage>(ref e.Buffer[0]);
                OnReceiveCompleted(message);
                Interlocked.Increment(ref ReceiveCounter);
            } while (!_socket.ReceiveAsync(e));
        }

//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
//        {
//#if NET461
//            if (!ExecutionContext.IsFlowSuppressed())
//            {
//                ExecutionContext.SuppressFlow();
//            }
//#endif
//            Interlocked.Increment(ref ReceiveCounter);
//            SocketAsyncEventArgs args = _argsPool.Allocate();

//            if (!_socket.ReceiveAsync(args))
//            {
//                // if false, the Complete won't be called
//                OnReceiveCompleted(this, args);
//            }

//            // TODO Process message here

//            _argsPool.Free(e);
//        }

        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;

                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;

                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SocketAsyncEventArgs EventArgsFactory()
        {
            var args = new SocketAsyncEventArgs();
            var buffer = new byte[_bufferLength];
            args.Completed += IO_Completed;
            args.SetBuffer(buffer, 0, _bufferLength);
            args.RemoteEndPoint = _inEndPoint;
            return args;
        }

        public void Dispose()
        {
            StopReceive();
            _socket?.Dispose();
        }
    }
}

//namespace MulticastTest
//{
//    public class Program
//    {
//        static void Main(string[] args)
//        {
//            new Program().Run();
//            Console.WriteLine("Press any key to exit...");
//            Console.ReadKey();
//        }

//        public void Run()
//        {
//            _waitFirstReadTiemout = new AutoResetEvent(false);
//            IPAddress lMulticastAddress = new IPAddress(0xFAFFFFEF);
//            IPEndPoint lRemoteEndPoint = new IPEndPoint(lMulticastAddress, 1900);

//            // Create sender socket
//            Socket lSendSocket = new Socket(AddressFamily.InterNetwork,
//                                 SocketType.Dgram,
//                                 ProtocolType.Udp);

//            // Allow to share the port 1900 with other applications
//            lSendSocket.SetSocketOption(SocketOptionLevel.Socket,
//                                    SocketOptionName.ReuseAddress,
//                                    true);

//            // Set TTL for multicast packets: socket needs to be bounded to do this
//            lSendSocket.SetSocketOption(SocketOptionLevel.IP,
//                                    SocketOptionName.MulticastTimeToLive,
//                                    2);

//            // Bind the socket to the local end point: this MUST be done before joining the multicast group
//            lSendSocket.Bind(new IPEndPoint(IPAddress.Loopback, 55236));

//            // Join the multicast group
//            lSendSocket.SetSocketOption(SocketOptionLevel.IP,
//                            SocketOptionName.MulticastLoopback,
//                            true);

//            lSendSocket.SetSocketOption(SocketOptionLevel.IP,
//                                    SocketOptionName.AddMembership,
//                                    new MulticastOption(lMulticastAddress));

//            // Create receiver and start its thread
//            Thread lReceiveThread = new Thread(ReceiveThread);
//            lReceiveThread.Start();

//            int i = 0;
//            while (!fStop)
//            {
//                if (i == 0)
//                    _waitFirstReadTiemout.WaitOne(10000);

//                byte[] lToSend = Encoding.ASCII.GetBytes(DateTime.Now.ToString("yyyyMMdd HHmmss"));
//                lSendSocket.SendTo(lToSend, lRemoteEndPoint);
//                Console.WriteLine("Sent #" + (i + 1) + ": " + DateTime.Now.ToString("yyyyMMdd HHmmss"));
//                Thread.Sleep(1000);
//                try
//                {
//                    if (Console.KeyAvailable || i >= 10)
//                        fStop = true;
//                }
//                catch (InvalidOperationException)
//                {
//                    fStop = i >= 10;
//                }
//                finally
//                {
//                    ++i;
//                }
//            }
//        }

//        private AutoResetEvent _waitFirstReadTiemout;

//        private bool fStop;

//        private void ReceiveThread()
//        {
//            Socket lSocket = new Socket(AddressFamily.InterNetwork,
//                                        SocketType.Dgram,
//                                        ProtocolType.Udp);

//            // Allow to share the port 1900 with other applications
//            lSocket.SetSocketOption(SocketOptionLevel.Socket,
//                                    SocketOptionName.ReuseAddress,
//                                    true);

//            // TTL not required here: we will only LISTEN on the multicast socket
//            // Bind the socket to the local end point: this MUST be done before joining the multicast group
//            lSocket.Bind(new IPEndPoint(IPAddress.Loopback, 1900));

//            // Join the multicast group

//            // If the local IP is a loopback one, enable multicast loopback
//            lSocket.SetSocketOption(SocketOptionLevel.IP,
//                        SocketOptionName.MulticastLoopback,
//                        true);

//            lSocket.SetSocketOption(SocketOptionLevel.IP,
//                                    SocketOptionName.AddMembership,
//                                    new MulticastOption(
//                                            new IPAddress(0xFAFFFFEF)));

//            lSocket.ReceiveTimeout = 1000;

//            byte[] lBuffer = new byte[65000];
//            int i = 0;
//            while (!fStop)
//            {
//                try
//                {
//                    int lReceived = lSocket.Receive(lBuffer);
//                    ++i;
//                    Console.WriteLine("Received #" + i + ": " + Encoding.ASCII.GetString(lBuffer, 0, lReceived));
//                }
//                catch (SocketException se)
//                {
//                    _waitFirstReadTiemout.Set();
//                    Console.WriteLine(se.ToString());
//                }
//            }
//        }
//    }
//}