using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<(KeyModifiers, Key), Action<string>> _globalKeyBindings;
    private IResultHandler _resultHandler;

    private readonly struct Entry(string line, int score)
    {
        public readonly string Line = line;
        public readonly int Score = score;
    }
    
    private class ThreadLocalData(Slab slab)
    {
        public PriorityQueue<Entry, int> Queue { get; } = new();
        public Slab Slab { get; } = slab;
    }
    
    private readonly ConcurrentBag<Slab> _slabPool = new();
    private const int MaxDegreeOfParallelism = 10;
    private int _selectedIndex;
    private bool _reading;
    private bool _searching;
    private int _numberOfItems;
    private bool _isWorking;
    private int _numberOfScoredItems;
    private const int MaxItems = 30;
    private readonly List<Chunk> _chunks = new();
    private readonly CancellationTokenSource _cancellation;
    private string _searchText;
    private readonly AutoResetEvent _restartSearchSignal = new(false);
    private bool _showResults;
    private ObservableCollection<HighlightedText> _displayItems = new();
    private readonly Slab _positionsSlab;
    private bool _isVisible;
    private string? _header;
    private bool _isHeaderVisible;
    private MenuDefinition _definition;

    public bool IsHeaderVisible
    {
        get => _isHeaderVisible;
        set
        {
            _isHeaderVisible = value;
            OnPropertyChanged();
        }
    }

    public string? Header
    {
        get => _header;
        set
        {
            _header = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<HighlightedText> DisplayItems
    {
        get => _displayItems;
        set => _displayItems = value;
    }

    public bool ShowResults
    {
        get => _showResults;
        set
        {
            _showResults = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if(value != _searchText)
            {
                _searchText = value;
                _restartSearchSignal.Set();
                OnPropertyChanged();
            }
        }
    }
    
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            OnPropertyChanged();
        }
    }

    private bool Searching
    {
        get => _searching;
        set
        {
            if (value == _searching) return;
            _searching = value;
            SetIsWorking();
        }
    }

    private bool Reading
    {
        get => _reading;
        set
        {
            if (value == _reading) return;
            _reading = value;
            SetIsWorking();
        }
    }
    
    public int NumberOfScoredItems
    {
        get => _numberOfScoredItems;
        set
        {
            if (value == _numberOfScoredItems) return;
            _numberOfScoredItems = value;
            OnPropertyChanged();
        }
    }

    public int NumberOfItems
    {
        get => _numberOfItems;
        set
        {
            if (value == _numberOfItems) return;
            _numberOfItems = value;
            OnPropertyChanged();
        }
    }

    public bool IsWorking
    {
        get => _isWorking;
        set
        {
            if (value == _isWorking) return;
            _isWorking = value;
            OnPropertyChanged();
        }
    }
    
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value == _isVisible) return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }
    
    public MainViewModel(Dictionary<(KeyModifiers, Key), Action<string>> globalKeyBindings)
    {
        _globalKeyBindings = globalKeyBindings;
        IsVisible = false;
        _positionsSlab = Slab.MakeDefault();
        _cancellation = new CancellationTokenSource();
        
        for (var i = 0; i < MaxDegreeOfParallelism; i++)
        {
            _slabPool.Add(Slab.MakeDefault());
        }
        
        Task.Run(async () => await ProcessLoop(), CancellationToken.None)
            .ContinueWith(t => 
            {
                if (t.IsFaulted)
                {
                    // Handle exception, e.g., log the error
                    var ex = t.Exception?.Flatten();
                    // Log or handle ex
                }
            });
        
        var firstChunk = new Chunk();
        _chunks.Add(firstChunk);
        SelectedIndex = -1;
        SearchText = string.Empty;
    }

    private void SetIsWorking()
    {
        IsWorking = Reading || Searching;
    }

    public void RunDefinition(MenuDefinition definition)
    {
        _definition = definition;
        IsVisible = true;
        DisplayItems.Clear();
        SearchText = string.Empty;
        IsHeaderVisible = definition.Header != null;
        Header = definition.Header;
        SelectedIndex = 0;
        
        if (definition.Command != null)
        {
            StartRead(definition.Command);
        }
        else if (definition.Function != null)
        {
            var enumerable = definition.Function();
            Task.Run(() =>
            {
                ReadEnumerable(enumerable);
            });
        }
        else if (definition.AsyncFunction != null)
        {
            Task.Run(async () =>
            {
                var reader = definition.AsyncFunction();
                await ReadFromSourceAsync(reader);
            });
        }
    }
    
    public async Task RunDefinitionAsync(MenuDefinition definition)
    {
        _definition = definition;
        IsVisible = true;
        DisplayItems.Clear();
        SearchText = string.Empty;
        IsHeaderVisible = definition.Header != null;
        Header = definition.Header;
        SelectedIndex = 0;
        
        if (definition.Command != null)
        {
            StartRead(definition.Command);
        }
        else if (definition.Function != null)
        {
            var enumerable = definition.Function();
            Task.Run(() =>
            {
                ReadEnumerable(enumerable);
            });
        }
        else if (definition.AsyncFunction != null)
        {
            var reader = definition.AsyncFunction();
            await ReadFromSourceAsync(reader);
        }
    }
    
    public void StartRead()
    {
        IsVisible = true;
        DisplayItems.Clear();
        Task.Run(() =>
        {
            var stream = Console.OpenStandardInput();
            ReadStream(stream);
        });
    }
    
    private void StartRead(string command)
    {
        //_minScore = minScore;
        SearchText = string.Empty;
        IsVisible = true;
        DisplayItems = new ObservableCollection<HighlightedText>();
        Task.Run(() =>
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments = $"/C {command}";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                using (var stream = process.StandardOutput.BaseStream)
                {
                    ReadStream(stream);
                }

                process.WaitForExit();
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task ProcessLoop()
    {
        Task previousTask = Task.CompletedTask;
        while (!_cancellation.Token.IsCancellationRequested)
        {
            _restartSearchSignal.WaitOne();
            await previousTask;
            previousTask = Render(CancellationToken.None);
        }
    }
    
    private Slab GetSlabFromPool()
    {
        if (_slabPool.TryTake(out var slab))
        {
            return slab;
        }

        throw new Exception("Number of outstanding slabs exceeded");
    }

    private void ReturnSlabToPool(Slab slab)
    {
        _slabPool.Add(slab);
    }

    private Task Render(CancellationToken ct)
    {
        var searchString = _searchText;
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, searchString, true);
        Searching = true;
        var globalQueue = new PriorityQueue<Entry, int>();

        var completeChunks = _chunks.Where(c => c.IsComplete).ToList();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism
        };

        var localQueues = new ConcurrentBag<PriorityQueue<Entry, int>>();

        var numberOfItemsWithScores = 0;
            
        Parallel.ForEach(completeChunks, parallelOptions, () => new ThreadLocalData(GetSlabFromPool()),
            (chunk, _, localData) =>
            {
                ItemScoreResult[]? cache = null;
                if (chunk.TryGetResultCache(searchString, ref cache, out var cacheSize))
                {
                    if (cache != null)
                    {
                        for (var i = 0; i < cacheSize; i++)
                        {
                            var item = cache[i];
                            Interlocked.Increment(ref numberOfItemsWithScores);
                            var entry = new Entry(chunk.Items[item.Index], item.Score);
                            localData.Queue.Enqueue(entry, item.Score);
                        }
                    }
                }
                else if (chunk.TryGetItemCache(searchString, ref cache, out cacheSize))
                {
                    chunk.SetQueryStringNoReset(searchString);
                    var added = 0;
                    for (var i = 0; i < cacheSize; i++)
                    {
                        if (cache != null)
                        {
                            var item = cache[i];
                            var line = chunk.Items[item.Index];
                            var score = FuzzySearcher.GetScore(line, pattern, localData.Slab);
                            if (score > _definition.MinScore)
                            {
                                Interlocked.Increment(ref numberOfItemsWithScores);
                                ProcessNewScore(line, score, localData);
                            }
                            if (score > 0)
                            {
                                chunk.SetResultCacheItemNoReset(item.Index, score, added);
                                added++;
                            }
                        }
                    }
                }
                else
                {
                    chunk.SetQueryStringNoReset(searchString);
                    var added = 0;
                    for (var i = 0; i < chunk.Items.Length; i++)
                    {
                        var line = chunk.Items[i];
                        var score = FuzzySearcher.GetScore(line, pattern, localData.Slab);
                        if (score > _definition.MinScore)
                        {
                            chunk.SetResultCacheItemNoReset(i, score, added);
                            Interlocked.Increment(ref numberOfItemsWithScores);
                            ProcessNewScore(line, score, localData);
                            added++;
                        }
                    }
                }

                return localData;
            },
            localQueue =>
            {
                ReturnSlabToPool(localQueue.Slab);
                localQueues.Add(localQueue.Queue);
            }
        );
        NumberOfScoredItems = numberOfItemsWithScores;

        foreach (var localQueue in localQueues)
        {
            while (localQueue.Count > 0)
            {
                var entry = localQueue.Dequeue();
                ProcessNewScore(entry, globalQueue);
            }
        }

        var topEntries = globalQueue.UnorderedItems
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.Element.Line.Length)
            .ThenBy(e => e.Element.Line, StringComparer.Ordinal)
            .Select(e =>
            {
                var pos = nfzf.FuzzySearcher.GetPositions(e.Element.Line, pattern, _positionsSlab);
                _positionsSlab.Reset();
                return new HighlightedText(e.Element.Line, pos);
            }).ToList();
             
        var previousIndex = SelectedIndex;
        DisplayItems.Clear();
        foreach (var item in topEntries)
        {
            DisplayItems.Add(item);
        }
        if (DisplayItems.Any() && previousIndex < 0)
        {
            previousIndex = 0;
        }
        else if (previousIndex > 0 && previousIndex > DisplayItems.Count - 1)
        {
            previousIndex = 0;
        }

        OnPropertyChanged(nameof(DisplayItems));

        Searching = false;
        ShowResults = DisplayItems.Count > 0;

        if (previousIndex > -1 && DisplayItems.Count - 1 >= previousIndex)
        {
            SelectedIndex = previousIndex;
        }

        return Task.CompletedTask;
    }
    
    private static void ProcessNewScore(Entry entry, PriorityQueue<Entry, int> queue)
    {
        if (entry.Line == null)
        {
            return;
        }
        
        if (queue.Count < MaxItems)
        {
            queue.Enqueue(entry, entry.Score);
        }
        else
        {
            var lowest = queue.Peek();
            if (entry.Score > lowest.Score)
            {
                queue.Dequeue();
                queue.Enqueue(entry, entry.Score);
            }
            else if (entry.Score == lowest.Score)
            {
                if (entry.Line.Length < lowest.Line.Length)
                {
                    queue.Dequeue();
                    queue.Enqueue(entry, entry.Score);
                }
                else if(entry.Line.Length == lowest.Line.Length)
                {
                    if (string.Compare(entry.Line, lowest.Line, StringComparison.Ordinal) < 0)
                    {
                        queue.Dequeue();
                        queue.Enqueue(entry, entry.Score);
                    }
                }
            }
        }
    }

    private static void ProcessNewScore(string line, int score, ThreadLocalData localData)
    {
        var entry = new Entry(line, score);
        ProcessNewScore(entry, localData.Queue);
    }
    
    private void ReadEnumerable(IEnumerable<string> lines)
    {
        SearchText = string.Empty;
        IsVisible = true;
        DisplayItems = new ObservableCollection<HighlightedText>();
        ReadFromSource(lines);
    }
    
    private async Task ReadEnumerableAsync(ChannelReader<string> channelReader)
    {
        SearchText = string.Empty;
        IsVisible = true;
        DisplayItems = new ObservableCollection<HighlightedText>();
        await ReadFromSourceAsync(channelReader);
    }
    
    private void ReadFromSource(IEnumerable<string> lines)
    {
        var numberOfItems = 0;
        NumberOfItems = 0;
        Reading = true;

        var currentChunk = _chunks.Last();

        foreach (var line in lines)
        {
            numberOfItems++;
            if (!currentChunk.TryAdd(line))
            {
                currentChunk = new Chunk();
                _chunks.Add(currentChunk);
                Chunk? lastFullChunk = _chunks.LastOrDefault(c => c.IsComplete);
                if (lastFullChunk != null)
                {
                    _restartSearchSignal.Set();
                }

                if (!currentChunk.TryAdd(line))
                {
                    throw new Exception("Could not add line to Chunk");
                }

                NumberOfItems = numberOfItems;
            }
        }

        NumberOfItems = numberOfItems;
        Reading = false;

        currentChunk.SetComplete();
        _restartSearchSignal.Set();
    }
    
    private async Task ReadFromSourceAsync(ChannelReader<string> channelReader)
    {
        var numberOfItems = 0;
        NumberOfItems = 0;
        Reading = true;

        var currentChunk = _chunks.Last();

        await foreach (var line in channelReader.ReadAllAsync())
        {
            numberOfItems++;
            if (!currentChunk.TryAdd(line))
            {
                currentChunk = new Chunk();
                _chunks.Add(currentChunk);
                Chunk? lastFullChunk = _chunks.LastOrDefault(c => c.IsComplete);
                if (lastFullChunk != null)
                {
                    _restartSearchSignal.Set();
                }

                if (!currentChunk.TryAdd(line))
                {
                    throw new Exception("Could not add line to Chunk");
                }

                NumberOfItems = numberOfItems;
            }
        }

        NumberOfItems = numberOfItems;
        Reading = false;

        currentChunk.SetComplete();
        _restartSearchSignal.Set();
    }
    
    private IEnumerable<string> ReadLines(StreamReader reader)
    {
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }

    private void ReadStream(Stream stream)
    {
        using (var reader = new StreamReader(stream))
        {
            ReadFromSource(ReadLines(reader));
        }
    }

    public async Task HandleKey(Key eKey, KeyModifiers eKeyModifiers)
    {
        if (eKeyModifiers == KeyModifiers.Control)
        {
            if (_definition.KeyBindings.TryGetValue((eKeyModifiers, eKey), out var action))
            {
                action(DisplayItems[SelectedIndex].Text);
            }
            else if (_globalKeyBindings.TryGetValue((eKeyModifiers, eKey), out var globalAction))
            {
                globalAction(DisplayItems[SelectedIndex].Text);
            }
        }
            
        switch (eKey)
        {
            case Key.Down:
                var nextIndex = Math.Min(DisplayItems.Count - 1, SelectedIndex + 1);
                SelectedIndex = nextIndex;
                break;
            case Key.Up:
                var previousIndex = Math.Max(0, SelectedIndex - 1);
                SelectedIndex = previousIndex;
                break;
            case Key.Escape:
                Close();
                break;
            case Key.Enter:
                if (SelectedIndex >= 0 && SelectedIndex < DisplayItems.Count)
                {
                    Close();
                    await _definition.ResultHandler.HandleAsync(DisplayItems[SelectedIndex].Text);
                }
                break;
        }
    }

    public void Close()
    {
        ShowResults = false;
        IsVisible = false;
        _chunks.Clear();
        _chunks.Add(new Chunk());
    }
}