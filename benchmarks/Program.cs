using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using nfzf;

namespace benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args)
    {
        _ = BenchmarkRunner.Run<AsciiFuzzyIndexBenchmarks>();
    }
}

public class CustomConfig : ManualConfig
{
    public CustomConfig()
    {
        AddJob(Job.Default
            .WithWarmupCount(1)
            .WithIterationCount(5));
    }
}

[Config(typeof(CustomConfig))]
[MemoryDiagnoser]
public class AsciiFuzzyIndexBenchmarks
{
    private string _input;
    private string _pattern;
    private bool _caseSensitive;
    private Slab _slab;

    [GlobalSetup]
    public void Setup()
    {
        _input = new string('a', 10000);
        _pattern = new string('a', 1000);
        _caseSensitive = true;
        _slab = Slab.MakeDefault();
    }

    [Benchmark]
    public void V2WithSlab()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfFuzzyMatchV2(true, inputSpan, patternSpan, _slab, null);
        _slab.Reset();
    }

    [Benchmark]
    public void PrefixMatch()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfPrefixMatch(true, inputSpan, patternSpan, _slab, null);
    }

    [Benchmark]
    public void SuffixMatch()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfSuffixMatch(true, inputSpan, patternSpan, _slab, null);
    }

    [Benchmark]
    public void FzfExactMatchNaive()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfExactMatchNaive(true, inputSpan, patternSpan, _slab, null);
    }

    [Benchmark]
    public void UsingSpan()
    {
        nfzf.FuzzySearcher.FuzzyMatchV1(true, _input, _pattern, _slab, null);
    }

    [Benchmark]
    public void ParsePattern()
    {
        nfzf.FuzzySearcher.ParsePattern(CaseMode.CaseSmart, _pattern, true);
    }
}