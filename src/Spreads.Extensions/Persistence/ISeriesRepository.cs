using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spreads;
using Spreads.Collections;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;

namespace Spreads.Persistence {

    // TODO! in CWT we should store lockreleaser with a finlizer that sends release command
    // not recommended way to use finalizer, but we could potentially leverage GC in a good way, together with CWT

    public interface ISeriesRepository {
        /// <summary>
        /// 
        /// </summary>
        IPersistentOrderedMap<K, V> WriteSeries<K, V>(string seriesId);

        /// <summary>
        /// Read-only series
        /// </summary>
        ISeries<K, V> ReadSeries<K, V>(string seriesId);
    }


    /// <summary>
    /// Non-generic object that knows how to apply a command to itself
    /// </summary>
    internal interface ICommandConsumer
    {
        void ApplyCommand(BaseCommand command);
    }

    public class SeriesRepository : ISeriesRepository {
        private readonly IPersistentStore _store;
        private readonly ISeriesNode _node;
        private readonly ConditionalWeakTable<object, object> _seriesLocks = new ConditionalWeakTable<object, object>();

        private readonly Dictionary<string, WeakReference<ICommandConsumer>> _series =
            new Dictionary<string, WeakReference<ICommandConsumer>>();

        public SeriesRepository(IPersistentStore store, ISeriesNode node) {
            _store = store;
            _node = node;
            _node.OnCommand += _node_OnCommand;
        }

        private void _node_OnCommand(BaseCommand command)
        {
            var seriesId = command.SeriesId;
            if (!_series.ContainsKey(seriesId)) return;
            ICommandConsumer consumer;
            if (_series[seriesId].TryGetTarget(out consumer))
            {
                consumer.ApplyCommand(command);
            }
        }

        // TODO! a wrapper that replicates any changes to the series by sending command via the series node

        private IPersistentOrderedMap<K, V> GetSeries<K, V>(string seriesId, bool writable = false)
        {
            var series = new RepositorySeriesWrapper<K, V>(_store.GetPersistentOrderedMap<K, V>(seriesId), _node);
            if (writable)
            {
                // TODO acquire exclusive global write lock
                _seriesLocks.Add(series, series.SyncRoot); // TODO! Add lock releaser that will send a lock release command in its finalizer
            }
            _series.Add(series.Id, new WeakReference<ICommandConsumer>(series));
            
            // subscribe to all updates for the series starting from the last available key
            Task.Run(() => _node.Send(new SubscribeFromCommand()
            {
                SeriesId = series.Id,
                FromKeyBytes = Serializer.Serialize<K>(series.IsEmpty ? default(K) : series.Last.Key)

            }));
            return series;
        }

        public IPersistentOrderedMap<K, V> WriteSeries<K, V>(string seriesId) {
            lock (_seriesLocks)
            {
                var writeSeries = GetSeries<K, V>(seriesId, true);
                
                return writeSeries;
            }
        }

        public ISeries<K, V> ReadSeries<K, V>(string seriesId) {
            // store returns reference to the same object as in the WriteSeries method above
            // TODO! check if there is any writer to the series and make the series synchronized (or do that always if there is any writer?)
            var readOnlySeries = _store.GetPersistentOrderedMap<K, V>(seriesId).ReadOnly();
            return readOnlySeries;
        }


        // THIS is chatty wrapper that is probably too slow
        // since we have only one writer per series, we could accumulate all mutations into a queue and 
        // send them in a separate thread

        /// <summary>
        /// This wrapper send all data mutation commands to the node
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        internal class RepositorySeriesWrapper<K, V> : IPersistentOrderedMap<K, V>, ICommandConsumer {
            private readonly IPersistentOrderedMap<K, V> _innerMap;
            private readonly ISeriesNode _node;
            private readonly Task _senderTask;
            private readonly ConcurrentQueue<BaseCommand> _commandQueue = new ConcurrentQueue<BaseCommand>();
            private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0, int.MaxValue);


            public RepositorySeriesWrapper(IPersistentOrderedMap<K, V> innerMap, ISeriesNode node) {
                _innerMap = innerMap;
                _node = node;
                _senderTask = Task.Run(async () => {
                    //var commandList = new List<BaseCommand>();
                    while (true) {
                        await _semaphore.WaitAsync(250); // TODO ensure this works without timeout
                        BaseCommand command;
                        //commandList.Clear();
                        while (_commandQueue.TryDequeue(out command)) {
                            var response = await _node.Send(command);
                            // TODO! what we do with response?
                            //commandList.Add(command);
                        }
                        await Task.Delay(50); // limit chattiness
                    }
                });
            }


            public void ApplyCommand(BaseCommand command)
            {
                var setCommand = command as SetCommand;
                if (setCommand != null)
                {
                    var sm = Serializer.Deserialize<SortedMap<K, V>>(setCommand.SerializedSortedMap);
                    foreach (var kvp in sm)
                    {
                        _innerMap[kvp.Key] = kvp.Value;
                    }
                    return;
                }

                var appendCommand = command as AppendCommand;
                if (appendCommand != null)
                {
                    var sm = Serializer.Deserialize<SortedMap<K, V>>(appendCommand.SerializedSortedMap);
                    _innerMap.Append(sm, appendCommand.AppendOption);
                    return;
                }

            }

            public V this[K key] {
                get {
                    return _innerMap[key];
                }

                set {
                    var sm = new SortedMap<K, V>(1) { { key, value } };
                    _commandQueue.Enqueue(new SetCommand()
                    {
                        SeriesId = _innerMap.Id,
                        SerializedSortedMap = Serializer.Serialize(sm)
                    });
                    _semaphore.Release();
                    _innerMap[key] = value;
                }
            }


            public void Add(K k, V v) {
                var sm = new SortedMap<K, V>(1) { { k, v } };
                _commandQueue.Enqueue(new SetCommand()
                {
                    SeriesId = _innerMap.Id,
                    SerializedSortedMap = Serializer.Serialize(sm)
                });
                _semaphore.Release();
                _innerMap.Add(k, v);
            }

            public void AddFirst(K k, V v) {
                var sm = new SortedMap<K, V>(1) { { k, v } };
                _commandQueue.Enqueue(new SetCommand()
                {
                    SeriesId = _innerMap.Id,
                    SerializedSortedMap = Serializer.Serialize(sm)
                });
                _semaphore.Release();
                _innerMap.AddFirst(k, v);
            }

            public void AddLast(K k, V v) {
                var sm = new SortedMap<K, V>(1) { { k, v } };
                _commandQueue.Enqueue(new SetCommand()
                {
                    SeriesId = _innerMap.Id,
                    SerializedSortedMap = Serializer.Serialize(sm)
                });
                _semaphore.Release();
                _innerMap.AddLast(k, v);
            }

            public int Append(IReadOnlyOrderedMap<K, V> appendMap, AppendOption option) {
                _commandQueue.Enqueue(new AppendCommand()
                {
                    SeriesId = _innerMap.Id,
                    AppendOption = option,
                    SerializedSortedMap = Serializer.Serialize(appendMap.ToSortedMap())
                });
                _semaphore.Release();
                return _innerMap.Append(appendMap, option);
            }

            public bool Remove(K k) {
                throw new NotImplementedException();
            }

            public bool RemoveFirst(out KeyValuePair<K, V> value) {
                throw new NotImplementedException();
            }

            public bool RemoveLast(out KeyValuePair<K, V> value) {
                throw new NotImplementedException();
            }

            public bool RemoveMany(K k, Lookup direction) {
                throw new NotImplementedException();
            }


            //////////////////////////////////// READ METHODS BELOW  ////////////////////////////////////////////


            V IReadOnlyOrderedMap<K, V>.this[K key] => _innerMap[key];

            public IComparer<K> Comparer => _innerMap.Comparer;

            public long Count => _innerMap.Count;

            public KeyValuePair<K, V> First => _innerMap.First;

            public string Id => _innerMap.Id;

            public bool IsEmpty => _innerMap.IsEmpty;

            public bool IsIndexed => _innerMap.IsIndexed;

            public bool IsMutable => _innerMap.IsMutable;

            public IEnumerable<K> Keys => _innerMap.Keys;

            public KeyValuePair<K, V> Last => _innerMap.Last;

            public object SyncRoot => _innerMap.SyncRoot;

            public IEnumerable<V> Values => _innerMap.Values;

            public void Dispose() {
                _innerMap.Dispose();
            }

            public void Flush() {
                _innerMap.Flush();
            }

            public V GetAt(int idx) {
                return _innerMap.GetAt(idx);
            }

            public ICursor<K, V> GetCursor() {
                return _innerMap.GetCursor();
            }

            public IAsyncEnumerator<KeyValuePair<K, V>> GetEnumerator() {
                return (_innerMap as IAsyncEnumerable<KeyValuePair<K, V>>).GetEnumerator();
            }

            public bool TryFind(K key, Lookup direction, out KeyValuePair<K, V> value) {
                return _innerMap.TryFind(key, direction, out value);
            }

            public bool TryGetFirst(out KeyValuePair<K, V> value) {
                return _innerMap.TryGetFirst(out value);
            }

            public bool TryGetLast(out KeyValuePair<K, V> value) {
                return _innerMap.TryGetLast(out value);
            }

            public bool TryGetValue(K key, out V value) {
                return _innerMap.TryGetValue(key, out value);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return (_innerMap as IEnumerable).GetEnumerator();
            }

            IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() {
                return (_innerMap as IEnumerable<KeyValuePair<K, V>>).GetEnumerator();
            }
        }

    }
}
