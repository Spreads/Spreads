using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Horology;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NUnit.Framework;

namespace Spreads.Core.Tests
{
    // ReSharper disable once InconsistentNaming
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class _BDN
    {
        protected int TestWarmupCount = 1;
        protected int TestIterationCount = 2;

        public class InProcessConfig : DebugConfig
        {
            private readonly int _warmupCount;
            private readonly int _iterationCount;

            public InProcessConfig(int warmupCount, int iterationCount)
            {
                _warmupCount = warmupCount;
                _iterationCount = iterationCount;
            }

            public override IEnumerable<Job> GetJobs()
            {
                return new[]
                {
#if DEBUG
                    JobMode<Job>.Default
                        .With(new InProcessEmitToolchain(TimeSpan.FromMinutes(1.0), false))
                        .WithIterationTime(TimeInterval.FromMilliseconds(1))
                        .WithWarmupCount(1)
                        .WithIterationCount(1)
                        .WithUnrollFactor(1)
#else
                    JobMode<Job>.Default
                        .With(new InProcessEmitToolchain(TimeSpan.FromMinutes(1.0), false))
                        .WithIterationTime(TimeInterval.FromMilliseconds(100))

                        .WithWarmupCount(_warmupCount)
                        .WithIterationCount(_iterationCount)
#endif
                };
            }
        }

        private bool _isRunningFromTests;

        [OneTimeSetUp]
        public void Setup()
        {
            _isRunningFromTests = true;
        }

        public bool IsInTests => _isRunningFromTests;

        private IConfig GetTestConfig() => new InProcessConfig(TestWarmupCount, TestIterationCount);

        protected virtual IConfig GetStandaloneConfig() => DefaultConfig.Instance.With(Job.ShortRun);

        [Test]
        public virtual void RunAll()
        {
            BenchmarkSwitcher.FromTypes(new[] {GetType()}).RunAll(IsInTests ? GetTestConfig() : GetStandaloneConfig());
        }

        public void Run(params string[] args)
        {
            BenchmarkSwitcher.FromTypes(new[] {GetType()}).Run(args, IsInTests ? GetTestConfig() : GetStandaloneConfig());
        }
    }
}