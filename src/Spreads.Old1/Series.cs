using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Spreads.Collections;
using Spreads.Collections.Internal;
using Spreads.Cursors.Internal;

namespace Spreads
{
    public readonly partial struct Cursor<TKey, TValue> : IDisposable
    {
        private readonly int _lifetimeVersion;
        private readonly CursorImpl _impl;

        internal Cursor(CursorImpl impl)
        {
            _lifetimeVersion = impl.LifetimeVersion;
            _impl = impl;
        }

        public void Dispose()
        {
            lock (_impl)
            {
                EnsureLifeTime();
                _impl.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureLifeTime()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (_lifetimeVersion != _impl.LifetimeVersion)
                    ThrowHelper.ThrowObjectDisposedException(nameof(Cursor<TKey, TValue>));
            }
        }
    }

    internal class SeriesImpl
    {
        
    }
    
    public readonly struct Series<TKey, TValue> : IDisposable
    {
        private readonly int _lifetimeVersion;
        
        internal readonly DataContainer? Container;
        internal readonly CursorImpl? Cursor;
        
        internal readonly DataBlock? DataRoot;
        
        
        // internal readonly ThreadLocal<CursorImpl>? _tlCursors;
        // internal readonly Cursor<TKey, TValue>? _tlCursors;
        
        public void Dispose()
        {
            Container?.Dispose();
            // if (_tlCursors)
            //     _tlCursors?.Dispose();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureLifeTime()
        {
            if (AdditionalCorrectnessChecks.Enabled)
            {
                if (_lifetimeVersion != _impl.LifetimeVersion)
                    ThrowHelper.ThrowObjectDisposedException(nameof(Cursor<TKey, TValue>));
            }
        }
    }
}