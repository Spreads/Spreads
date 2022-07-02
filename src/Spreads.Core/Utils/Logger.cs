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
        private static readonly ILoggerFactory _noopFactory = new NoopLoggerFactory();
        private static ILoggerFactory _factory = _noopFactory;

        public static ILogger ForType<T>() => Factory.CreateLogger<T>();
        public static ILogger ForType(Type type) => Factory.CreateLogger(type);

        public static ILoggerFactory Factory
        {
            get => _factory;
            set
            {
                if (value == null!) value ??= _noopFactory;
                Interlocked.Exchange(ref _factory, value)?.Dispose();
            }
        }

        public static void SetNoopLogger() => Factory = new NoopLoggerFactory();

        private class NoopLoggerFactory : ILoggerFactory, ILogger
        {
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

            public bool IsEnabled(LogLevel logLevel) => false;

            public IDisposable BeginScope<TState>(TState state) => this;
        }

        public static void SetConsoleLogger() => Factory = new FastConsoleLoggerFactory();

        /// <summary>
        /// Flushes instantly, good for tests.
        /// </summary>
        private class FastConsoleLoggerFactory : ILoggerFactory, ILogger
        {
            public void Dispose()
            {
            }

            public ILogger CreateLogger(string categoryName) => this;

            public void AddProvider(ILoggerProvider provider)
            {
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) => Console.WriteLine(formatter(state, exception));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => this;
        }
    }
}
