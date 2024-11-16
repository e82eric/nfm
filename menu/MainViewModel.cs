using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using nfzf;

namespace nfm.menu;

public sealed class MainViewModel : INotifyPropertyChanged
{
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

    public ObservableCollection<HighlightedText> DisplayItems
    {
        get => _displayItems;
        set
        {
            if (Equals(value, _displayItems)) return;
            _displayItems = value;
            OnPropertyChanged();
        }
    }

    public bool ShowResults
    {
        get => _showResults;
        set
        {
            if (value == _showResults) return;
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
    public MainViewModel()
    {
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
        _searchText = string.Empty;
    }

    private void SetIsWorking()
    {
        IsWorking = Reading || Searching;
    }

    public void StartRead()
    {
        Task.Run(() =>
        {
            var stream = Console.OpenStandardInput();
            ReadStream(stream);
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
    
    private async Task Render(CancellationToken ct)
    {
        var searchString = _searchText;
        var pattern = nfzf.FuzzySearcher.ParsePattern(CaseMode.CaseSmart, searchString, true);
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
                            var score = nfzf.FuzzySearcher.GetScore(line, pattern, localData.Slab);
                            if (score > 50)
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
                        var score = nfzf.FuzzySearcher.GetScore(line, pattern, localData.Slab);
                        if (score > 50)
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
                if (globalQueue.Count < MaxItems)
                {
                    globalQueue.Enqueue(entry, entry.Score);
                }
                else
                {
                    var lowest = globalQueue.Peek();
                    if (entry.Score > lowest.Score)
                    {
                        globalQueue.Dequeue();
                        globalQueue.Enqueue(entry, entry.Score);
                    }
                    else if(entry.Score == lowest.Score)
                    {
                        globalQueue.Enqueue(entry, entry.Score);
                    }
                }
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
             
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
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

            SelectedIndex = previousIndex;
        }, DispatcherPriority.Background);
        Searching = false;
        ShowResults = DisplayItems.Count > 0;
    }

    private static void ProcessNewScore(string line, int score, ThreadLocalData localData)
    {
        var entry = new Entry(line, score);
        if (localData.Queue.Count < MaxItems)
        {
            localData.Queue.Enqueue(entry, score);
        }
        else
        {
            var lowest = localData.Queue.Peek();
            if (score > lowest.Score)
            {
                localData.Queue.Dequeue();
                localData.Queue.Enqueue(entry, score);
            }
            else if (score == lowest.Score)
            {
                if (line.Length < lowest.Line.Length)
                {
                    localData.Queue.Dequeue();
                    localData.Queue.Enqueue(entry, score);
                }
                else if(line.Length == lowest.Line.Length)
                {
                    if (string.Compare(line, lowest.Line, StringComparison.Ordinal) < 0)
                    {
                        localData.Queue.Dequeue();
                        localData.Queue.Enqueue(entry, score);
                    }
                }
            }
        }
    }

    private void ReadStream(Stream stream)
    {
        var numberOfItems = 0;
        NumberOfItems = 0;
        Reading = true;
        var currentChunk = _chunks.Last();
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
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
        }

        NumberOfItems = numberOfItems;
        Reading = false;

        currentChunk.SetComplete();
        
        _restartSearchSignal.Set();
    }

    public void HandleKey(Key eKey)
    {
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
                Environment.Exit(0);
                break;
            case Key.Enter:
                if (SelectedIndex >= 0 && SelectedIndex < DisplayItems.Count)
                {
                    Console.WriteLine(DisplayItems[SelectedIndex].Text);
                    Environment.Exit(0);
                }
                break;
        }
    }
}