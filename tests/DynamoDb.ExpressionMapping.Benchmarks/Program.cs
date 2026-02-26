using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Running;

var config = ManualConfig.Create(DefaultConfig.Instance)
    .AddExporter(JsonExporter.FullCompressed);
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
