using System.Buffers;
using System.Diagnostics;
using System.Threading.Channels;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Validators;
using nfzf;
using nfzf.FileSystem;

namespace benchmarks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

class Program
{
    static void Main(string[] args)
    {
        //var summary = BenchmarkRunner.Run<AsciiFuzzyIndexBenchmarks>(new DebugInProcessConfig());
        var summary = BenchmarkRunner.Run<AsciiFuzzyIndexBenchmarks>();
    }
}

public class CustomConfig : ManualConfig
{
    public CustomConfig()
    {
        AddJob(Job.Default
            .WithWarmupCount(1)   // Number of warmup iterations (optional)
            .WithIterationCount(5)); // Limit the total number of runs to 5
    }
}


public class AllowNonOptimized : ManualConfig
{
    public AllowNonOptimized()
    {
        Add(JitOptimizationsValidator.DontFailOnError); // ALLOW NON-OPTIMIZED DLLS

        Add(DefaultConfig.Instance.GetLoggers().ToArray()); // manual config has no loggers by default
        Add(DefaultConfig.Instance.GetExporters().ToArray()); // manual config has no exporters by default
        Add(DefaultConfig.Instance.GetColumnProviders().ToArray()); // manual config has no columns by default
    }
}

[Config(typeof(CustomConfig))]
[MemoryDiagnoser]
public class AsciiFuzzyIndexBenchmarks
{
    private string _input;
    private string _pattern;
    private bool _caseSensitive;
    private int[] _pos;
    private ArrayPool<int> _pool;
    private ArrayPool<char> _textPool;
    private Slab _slab;
    private Pattern _pat;
    private byte[] _testData;
    private ArrayPool<string> _arrayPool;


    [GlobalSetup]
    public void Setup()
    {
        // Initialize your test data here
        _input = new string('a', 10000);      // Input string of length 10,000
        //_input = "aabcd";
        _pattern = new string('a', 1000);     // Pattern string of length 1,000
        //_pattern = "abc";
        _pos = new int[_pattern.Length];
        _caseSensitive = true;
        _pool = ArrayPool<int>.Shared;
        _textPool = ArrayPool<char>.Shared;
        _pat = nfzf.FuzzySearcher.ParsePattern(CaseMode.CaseSmart, _pattern, true);
        
        //int[] warmUpSizes = { 128, 256, 512, 1024, 2048 };
        //int[] warmUpSizes = { _input.Length, _pattern.Length, _input.Length * _pattern.Length, 1000 };
        //WarmUpArrayPool(_textPool, warmUpSizes, 5);
        //WarmUpArrayPool(_pool, warmUpSizes, 5);
        //_slab = new Slab(100 * 1024 * 10000, 2048 * 100);
        _slab = Slab.MakeDefault();
         //_testData = File.ReadAllBytes("random_file_paths.txt");
        _walker = new FileWalker();
        _walker2 = new FileWalker2();
        _walker5 = new FileWalker5();
        _walker6 = new FileWalker6();
        _walker7 = new FileWalker8();
        _walker9 = new FileWalker9();
        _arrayPool = ArrayPool<string>.Create(maxArrayLength: 12, maxArraysPerBucket: 50);
    }

    //[Benchmark]
    //public async Task CurrentState()
    //{
    //    var c = Channel.CreateUnbounded<string>();
    //    var writeTask = _walker.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);
    //    await writeTask;
    //}
    
    //[Benchmark]
    //public async Task FileWalker2()
    //{
    //    var c = Channel.CreateUnbounded<(string, string)>();
    //    var writeTask = _walker2.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);
    //    await writeTask;
    //}
    
    //[Benchmark]
    public async Task FileWalker5()
    {
        var c = Channel.CreateUnbounded<(string[], string)>();
        var writeTask = _walker5.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);
        await writeTask;
    }
    
    //[Benchmark]
    public async Task FileWalker6()
    {
        var c = Channel.CreateUnbounded<string[]>();
        var writeTask = _walker6.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);
        await writeTask;
    }
    
    [Benchmark]
    public async Task FileWalker8()
    {
        var c = Channel.CreateUnbounded<FileSystemNode>();
        var writeTask = _walker7.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);
        await writeTask;
    }
    
    [Benchmark]
    public async Task FileWalker9()
    {
        var c = Channel.CreateUnbounded<FileSystemNode>();
        var writeTask = _walker9.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);
        await writeTask;
    }
    
    //[Benchmark]
    public async Task FileWalker1_WithScore()
    {
        var c = Channel.CreateUnbounded<string>();
        var writeTask = _walker.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);

        await foreach (var item in c.Reader.ReadAllAsync())
        {
            var score = FuzzySearcher.GetScore(item, _pat, _slab);
        }
        
        await writeTask;
    }
    
    //[Benchmark]
    public async Task FileWalker2_WithScore()
    {
        var c = Channel.CreateUnbounded<(string, string)>();
        var writeTask = _walker2.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);

        await foreach (var item in c.Reader.ReadAllAsync())
        {
            var score = FuzzySearcher.GetScore(item.Item1, item.Item2, _pat, _slab);
        }
        
        await writeTask;
    }
    
   // [Benchmark]
    public async Task FileWalker5_WithScore()
    {
        var c = Channel.CreateUnbounded<(string[], string)>();
        var writeTask = _walker5.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);

        await foreach (var item in c.Reader.ReadAllAsync())
        {
            var score = FuzzySearcher.GetScore(item.Item1, item.Item2, _pat, _slab);
        }
        
        await writeTask;
    }
    
    //[Benchmark]
    //public async Task FileWalker6_WithScore()
    //{
    //    var c = Channel.CreateUnbounded<string[]>();
    //    var writeTask = _walker6.StartScanForDirectoriesAsync([@"c:"], c.Writer, int.MaxValue, false, false, CancellationToken.None);

    //    await foreach (var item in c.Reader.ReadAllAsync())
    //    {
    //        var score = FuzzySearcher.GetScore(item, _pat, _slab);
    //    }
    //    
    //    await writeTask;
    //}
    
    //[Benchmark]
    public void DotNetVersion()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "C:\\Users\\eric\\src\\nfzf\\ConsoleTest\\bin\\Release\\net8.0\\win-x64\\publish\\ConsoleTest.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return;
        
        process.StandardInput.BaseStream.Write(_testData, 0, _testData.Length);
        process.StandardInput.Close();
        
        // Read all output to ensure process completes
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
    
    //[Benchmark]
    public void FzfVersion()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "fzf",
            Arguments = $"--filter=doe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return;
        
        process.StandardInput.BaseStream.Write(_testData, 0, _testData.Length);
        process.StandardInput.Close();
        
        // Read all output to ensure process completes
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
    
    //[Benchmark]
    public void V2WithSlab()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfFuzzyMatchV2(true, inputSpan, patternSpan, _slab, null);
        _slab.Reset();
    }
    
    //[Benchmark]
    //public void V2WithPool()
    //{
    //    var inputSpan = _input.AsSpan();
    //    var patternSpan = _pattern.AsSpan();
    //    nfzf.nfzf.FzfFuzzyMatchV2EricPool(true, inputSpan, patternSpan, _pool, _textPool);
    //}
    
    //[Benchmark]
    //public void V2()
    //{
    //    var inputSpan = _input.AsSpan();
    //    var patternSpan = _pattern.AsSpan();
    //    nfzf.nfzf.FzfFuzzyMatchV2Eric(true, inputSpan, patternSpan);
    //}
    
    //[Benchmark]
    public void PrefixMatch()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfPrefixMatch(true, inputSpan, patternSpan, _slab, null);
    }
    
    //[Benchmark]
    public void SuffixMatch()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfSuffixMatch(true, inputSpan, patternSpan, _slab, null);
    }
    
    //[Benchmark]
    public void FzfExactMatchNaive()
    {
        var inputSpan = _input.AsSpan();
        var patternSpan = _pattern.AsSpan();
        nfzf.FuzzySearcher.FzfExactMatchNaive(true, inputSpan, patternSpan, _slab, null);
    }
    
    //[Benchmark]
    public void UsingSpan()
    {
        nfzf.FuzzySearcher.FuzzyMatchV1(true, _input, _pattern, _slab, null);
    }

    //[Benchmark]
    public void ParsePattern()
    {
        nfzf.FuzzySearcher.ParsePattern(CaseMode.CaseSmart, _pattern, true);
    }
}
