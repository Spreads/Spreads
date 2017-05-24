// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Spreads.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spreads.Cursors.Experimental
{
    /// <summary>
    /// Base abstract class for cursor series (objects that implement both <see cref="IReadOnlySeries{TKey, TValue}"/> and <see cref="ICursor{TKey, TValue}"/>).
    /// </summary>
    [Obsolete("Use CursorSeries")]
    internal abstract class AbstractCursorSeries<TKey, TValue, TCursor> : Series<TKey, TValue> // TODO, ISpecializedSeries<TKey, TValue, TCursor>
        where TCursor : AbstractCursorSeries<TKey, TValue, TCursor>, ISpecializedCursor<TKey, TValue, TCursor>, new()
    {
        /// <summary>
        /// NB the idea is that the class is first used when the first instance is created
        /// if we do not expose non-private constructors other than the empty one then
        /// in the factory <see cref="GetUninitializedStatic"/> methods we could try to reuse this cached instance.
        /// </summary>
        internal static TCursor _reusable = new TCursor { _inUse = -1 };

        /// <summary>
        /// Set to 1 when this instance is initialized as a cursor via <see cref="Initialize"/>.
        /// Set to 0 when this instance is created and is ready to be used as a cursor.
        /// Set to -1 when this instance is stored for reuse (as if it is GCed, such instance could only be used via <see cref="GetUninitializedStatic"/> factory).
        /// </summary>
        internal int _inUse;

        internal CursorState State;

        /// <inheritdoc />
        public IReadOnlySeries<TKey, TValue> Source => this;

        /// <summary>
        /// Get strongly-typed enumerator.
        /// </summary>
        /// <returns>An initialized <typeparamref name="TCursor"/> instance.</returns>
        public TCursor GetEnumerator()
        {
            var clone = Initialize();
            return clone;
        }

        /// <summary>
        /// Create an initialized copy of <typeparamref name="TCursor"/>. It must be safe to call this method on
        /// a previously disposed CursorSeries instances, e.g. in the case of re-using a series
        /// as a cursor or in the pooling case.
        /// </summary>
        public abstract TCursor Initialize();

        #region Series overrides

        /// <inheritdoc />
        public sealed override ICursor<TKey, TValue> GetCursor()
        {
            return new BaseCursorAsync<TKey, TValue, TCursor>(Initialize);
        }

        /// <inheritdoc />
        public override bool IsEmpty
        {
            get
            {
                using (var c = Initialize())
                {
                    return !c.MoveFirst();
                }
            }
        }

        /// <inheritdoc />
        public sealed override KeyValuePair<TKey, TValue> First
        {
            get
            {
                using (var c = Initialize())
                {
                    return c.MoveFirst() ? c.Current : throw new InvalidOperationException("Series is empty");
                }
            }
        }

        /// <inheritdoc />
        public sealed override KeyValuePair<TKey, TValue> Last
        {
            get
            {
                using (var c = Initialize())
                {
                    return c.MoveLast() ? c.Current : throw new InvalidOperationException("Series is empty");
                }
            }
        }

        /// <inheritdoc />
        public override TValue GetAt(int idx)
        {
            // NB call to this.NavCursor.Source.GetAt(idx) is recursive (=> SO) and is logically wrong
            if (idx < 0) throw new ArgumentOutOfRangeException(nameof(idx));
            using (var c = Initialize())
            {
                if (!c.MoveFirst())
                {
                    throw new KeyNotFoundException();
                }
                for (int i = 0; i < idx - 1; i++)
                {
                    if (!c.MoveNext())
                    {
                        throw new KeyNotFoundException();
                    }
                }
                return c.CurrentValue;
            }
        }

        /// <inheritdoc />
        public sealed override bool TryFind(TKey key, Lookup direction, out KeyValuePair<TKey, TValue> value)
        {
            using (var c = Initialize())
            {
                if (c.MoveAt(key, direction))
                {
                    value = c.Current;
                    return true;
                }
                value = default(KeyValuePair<TKey, TValue>);
                return false;
            }
        }

        /// <inheritdoc />
        public sealed override bool TryGetFirst(out KeyValuePair<TKey, TValue> value)
        {
            using (var c = Initialize())
            {
                if (c.MoveFirst())
                {
                    value = c.Current;
                    return true;
                }
                value = default(KeyValuePair<TKey, TValue>);
                return false;
            }
        }

        /// <inheritdoc />
        public sealed override bool TryGetLast(out KeyValuePair<TKey, TValue> value)
        {
            using (var c = Initialize())
            {
                if (c.MoveLast())
                {
                    value = c.Current;
                    return true;
                }
                value = default(KeyValuePair<TKey, TValue>);
                return false;
            }
        }

        /// <inheritdoc />
        public sealed override IEnumerable<TKey> Keys
        {
            get
            {
                using (var c = Initialize())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentKey;
                    }
                }
            }
        }

        /// <inheritdoc />
        public sealed override IEnumerable<TValue> Values
        {
            get
            {
                using (var c = Initialize())
                {
                    while (c.MoveNext())
                    {
                        yield return c.CurrentValue;
                    }
                }
            }
        }

        #endregion Series overrides

        /// <summary>
        /// Get pooled or new <typeparamref name="TCursor"/> uninitialized instance.
        /// </summary>
        [NotNull]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TCursor GetUninitializedInstance()
        {
            var inUse = Interlocked.CompareExchange(ref _inUse, 1, 0);
            switch (inUse)
            {
                // we have taken the use ownership
                case 0:
                    Debug.Assert(State == CursorState.None);
                    Debug.Assert(_inUse == 1);
                    return (TCursor)this;

                // it was already used
                case 1:
                    var instance = GetUninitializedStatic();
                    instance._inUse = 1;
                    return instance;

                // it was completely disposed
                case -1:
                    ThrowHelper.ThrowInvalidOperationException($"CursorSeries {typeof(TCursor).Name} is diposed.");
                    break;

                default:
                    ThrowHelper.ThrowInvalidOperationException($"Wrong _inUse state in {typeof(TCursor).Name}.");
                    break;
            }
            return default(TCursor);
        }

        [NotNull]
        internal static TCursor GetUninitializedStatic()
        {
            var reusable = _reusable;
            if (!ReferenceEquals(reusable, null) && ReferenceEquals(reusable,
                    Interlocked.CompareExchange(ref _reusable, null, reusable)))
            {
                // _inUse == -1 means that the object is as "if not created", it is dead, void...
                Debug.Assert(reusable._inUse == -1);
                Debug.Assert(reusable.State == CursorState.None);

                reusable._inUse = 0;

                // now reusable has _inUse == 0 and is in the same state as `new TCursor()`, just return it
                return reusable;
            }

            return new TCursor();
        }

        /// <summary>
        /// Release a <see cref="AbstractCursorSeries{TKey,TValue,TCursor}"/> instance so that it could be reused later.
        /// </summary>
        internal static void ReleaseInstance([NotNull]TCursor instance)
        {
            var inUse = Interlocked.CompareExchange(ref instance._inUse, 0, 1);
            switch (inUse)
            {
                case 1:
                    break;

                case 0:
                    inUse = Interlocked.CompareExchange(ref instance._inUse, -1, 0);
                    if (inUse == 0)
                    {
                        // replaced _inUse with -1, now ISeries is in diposed state
                        Interlocked.Exchange(ref _reusable, instance);
                    }
                    break;
            }
        }

        // Derived classes will use this as the implementation for the ICursor method.
        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken) => throw new NotSupportedException("Async MoveNext should use BaseCursor via CursorSeries");
    }
}