// This Source Code Form is subject to the terms of the Mozilla Public
//  License, v. 2.0. If a copy of the MPL was not distributed with this
//  file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Spreads.Utils
{
    public static class Logger
    {
        private static readonly ILoggerFactory NoopFactory = new NoopLoggerFactory();
        private static ILoggerFactory _factory = NoopFactory;

        public static ILoggerFactory Factory
        {
            get => _factory;
            set
            {
                if (value == null!)
                    Interlocked.Exchange(ref _factory, NoopFactory);
                else
                    Interlocked.Exchange(ref _factory, value)?.Dispose();
            }
        }

        private class NoopLoggerFactory : ILoggerFactory, ILogger
        {
            private class NoopDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }

            public void Dispose()
            {
            }

            public ILogger CreateLogger(string categoryName)
            {
                return this;
            }

            public void AddProvider(ILoggerProvider provider)
            {

            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return false;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return new NoopDisposable();
            }
        }
    }
}
