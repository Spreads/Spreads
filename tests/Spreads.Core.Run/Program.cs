using Spreads.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Spreads.Serialization;

namespace Spreads.Core.Run
{
    public class XXX
    {
        private WaitCallback callback;
        private Action<object> action;
        private object state;

        public XXX()
        {
            callback = new WaitCallback(o => action(o));
        }
    }

    public sealed class TestVTS : IValueTaskSource
    {
        private object _syncRoot;

        // private List<(Action<object>, object)> _finalizing = new List<(Action<object>, object)>();
        //private Action<object> _continuation;
        //private object _state;

        private ConcurrentQueue<(Action<object>, object)> _queue = new ConcurrentQueue<(Action<object>, object)>();
        // private Queue<(Action<object>, object)> _queue2 = new Queue<(Action<object>, object)>();

        public TestVTS()//
        {
            // _queue = new ConcurrentQueue<(Action<object>, object)>();
            //_continuation = null;
            //_state = null;
        }

        public object SyncRoot
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_syncRoot == null)
                {
                    Interlocked.CompareExchange(ref _syncRoot, new object(), null);
                }
                return _syncRoot;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask GetValueTask()
        {
            return new ValueTask(this, 0);
        }

        //public Task GetValueTask2()
        //{
        //    _tcs = new TaskCompletionSource<object>();
        //    _tcs.Task.GetAwaiter().on
        //    return _tcs.Task;
        //}
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public void Notify2()
        //{
        //    _tcs.SetResult(null);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTaskSourceStatus GetStatus(short token)
        {
            // Console.WriteLine("GetStatus");
            return ValueTaskSourceStatus.Pending;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _queue.Enqueue((continuation, state));

            //var idx = Interlocked.Increment(ref _idx);
            //_active[idx - 1] = (continuation, state);

            //var prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);
            //if (prevContinuation != null)
            //{
            //    lock (SyncRoot)
            //    {
            //        if (_queue == null)
            //        {
            //            _queue = new ConcurrentQueue<(Action<object>, object)>();
            //        }
            //    }
            //    _queue.Enqueue((continuation, state));
            //}
            //else
            //{
            //    _continuation = continuation;
            //    _state = state;
            //}

            //Notify();
            //Console.WriteLine("OnCompleted");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetResult(short token)
        {
            // Console.WriteLine("GetResult");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Notify()
        {
            while (_queue.TryDequeue(out var item))
            {
                // item.Item1(item.Item2);
                //var a = item.Item1;
                //var wcb = Unsafe.As<Action<object>, WaitCallback>(ref a);
                ThreadPool.QueueUserWorkItem(item.Item1, item.Item2, true);
                //var a = item.Item1;
                //var wcb = Unsafe.As<Action<object>, WaitCallback>(ref a);
                //ThreadPool.UnsafeQueueUserWorkItem(wcb, item.Item2);
            }

            //Interlocked.Exchange(ref _active, _finalizing);
            //lock (_active)
            //{
            //    foreach (var item in _active)
            //    {
            //        item.Item1(item.Item2);
            //    }
            //}
            //while (true)
            //{
            //    Action<object> continuation;
            //    object state;

            //    lock (SyncRoot)
            //    {
            //        if (_continuation != null)
            //        {
            //            continuation = _continuation;
            //            state = _state;
            //            _continuation = null;
            //            _state = null;
            //        }
            //        else
            //        {
            //            break;
            //        }
            //    }

            //    continuation(state);
            //}
            //var prevContinuation = Interlocked.CompareExchange(ref _continuation, continuation, null);

            //ConcurrentQueue<(Action<object>, object)> queue;
            //lock (SyncRoot)
            //{
            //    queue = _queue;
            //}

            //var cont = _continuation;
            //var state = _state;

            //if (cont != null && state != null)
            //{
            //    cont.Invoke(state);
            //    Interlocked.Exchange(ref _continuation, null);
            //}

            //var queue = Volatile.Read(ref _queue);
            //if (queue != null)
            //{
            //    while (queue.TryDequeue(out var item))
            //    {
            //        item.Item1(item.Item2);
            //        //var a = item.Item1;
            //        //var wcb = Unsafe.As<Action<object>, WaitCallback>(ref a);
            //        //ThreadPool.UnsafeQueueUserWorkItem(wcb, item.Item2);
            //    }
            //}
        }
    }

    internal class Program
    {
        public static async Task TestVTS()
        {
            var count = 1_000_000;
            var vts = new TestVTS();

            vts.GetValueTask().GetAwaiter().UnsafeOnCompleted(() =>
            {
                Console.WriteLine("OnCompleted");
            });

            //var _ = Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        await Task.Delay(10);
            //        vts.Notify();
            //    }

            //});
            for (int r = 0; r < 30; r++)
            {
                var finished = false;
                using (Benchmark.Run("mna", count))
                {
                    var __ = Task.Run(async () =>
                    {
                        while (!finished)
                        {
                            vts.Notify();
                        }
                    });

                    for (int i = 0; i < count; i++)
                    {
                        await vts.GetValueTask();
                    }

                    finished = true;
                }
            }

            Benchmark.Dump();

            async ValueTask AwaitVT(ValueTask vt)
            {
                await vt;
            }

            var x = AwaitVT(default(ValueTask));
        }

        private static void Main(string[] args)
        {

            //var dirs = Directory.GetFileSystemEntries("/localdata", "*", SearchOption.AllDirectories);
            //foreach (var dir in dirs)
            //{
            //    Console.WriteLine(dir);
            //}

            // TestVTS().Wait();
            var test = new Spreads.Core.Tests.Threading.SpinningThreadpoolTests();
            test.ThreadPoolPerformanceBenchmark();
            //BinaryLz4();
            // Zstd();
            Console.ReadLine();
        }

        //static unsafe void LZ4()
        //{
        //    var rng = new Random();

        //    var dest = (Memory<byte>)new byte[1000000];
        //    var buffer = dest;
        //    var handle = buffer.Pin();
        //    var ptr = (IntPtr)handle.Pointer;

        //    var source = new decimal[10000];
        //    for (var i = 0; i < 10000; i++)
        //    {
        //        source[i] = i;
        //    }

        //    var len = BinarySerializer.Write(source, ref buffer, 0, null,
        //        SerializationFormat.BinaryLz4);

        //    Console.WriteLine($"Useful: {source.Length * 16}");
        //    Console.WriteLine($"Total: {len}");

        //    var destination = new decimal[10000];

        //    var len2 = BinarySerializer.Read(buffer, out destination);

        //    if (source.SequenceEqual(destination))
        //    {
        //        Console.WriteLine("BinaryLz4 OK");
        //    }
        //    handle.Dispose();

        //}

        static unsafe void Zstd()
        {
            var rng = new Random();

            var dest = (Memory<byte>)new byte[1000000];
            var buffer = dest;
            var handle = buffer.Pin();
            var ptr = (IntPtr)handle.Pointer;

            var source = new decimal[10000];
            for (var i = 0; i < 10000; i++)
            {
                source[i] = i;
            }

            var len = BinarySerializer.Write(source, ref buffer, null,
                SerializationFormat.BinaryZstd);

            Console.WriteLine($"Useful: {source.Length * 16}");
            Console.WriteLine($"Total: {len}");

            var destination = new decimal[10000];

            var len2 = BinarySerializer.Read(buffer, out destination);

            if (source.SequenceEqual(destination))
            {
                Console.WriteLine("BinaryZstd OK");
            }
            handle.Dispose();
        }
    }
}