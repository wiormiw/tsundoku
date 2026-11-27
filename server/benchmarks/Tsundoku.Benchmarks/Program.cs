using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Tsundoku.Benchmarks;

// In process: BenchmarkDotNet 0.15.8 (latest stable) does not recognise net11.0 and its out-of-process
// toolchain fails validation; move to the default toolchain when a release does.
var config = DefaultConfig.Instance.AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
BenchmarkSwitcher.FromAssembly(typeof(FoldBenchmarks).Assembly).Run(args, config);
