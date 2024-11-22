using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

internal sealed class AsyncAutoResetEvent
{
    private static readonly Task s_completed = Task.FromResult(true);
    private readonly object _lock = new();
    private TaskCompletionSource<bool>? _tcs;
    private bool _signaled;

    public Task WaitAsync()
    {
        lock (_lock)
        {
            if (_signaled)
            {
                _signaled = false;
                return s_completed;
            }
            return (_tcs = new TaskCompletionSource<bool>()).Task;
        }
    }

    public void Set()
    {
        TaskCompletionSource<bool>? tcs = null;
        lock (_lock)
        {
            if (_tcs != null)
            {
                tcs = _tcs;
                _tcs = null;
            }
            else
                _signaled = true;
        }
        tcs?.SetResult(true);
    }
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<(KeyModifiers, Key), Action<string>> _globalKeyBindings;

    private readonly struct Entry(string line, int score)
    {
        public readonly string Line = line;
        public readonly int Score = score;
    }
    
    private class ThreadLocalData(Slab slab)
    {
        public List<Entry> Entries { get; } = new(MaxItems);
        public Slab Slab { get; } = slab;
    }
    
    private readonly ConcurrentBag<ThreadLocalData> _localResultsPool = new();
    private int MaxDegreeOfParallelism = Environment.ProcessorCount;
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
    private readonly AsyncAutoResetEvent _restartSearchSignal = new();
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
            _localResultsPool.Add(new ThreadLocalData(Slab.MakeDefault()));
        }

        //Start the processing loop.  need to handle the task better.
        _ = ProcessLoop();
        
        var firstChunk = new Chunk();
        _chunks.Add(firstChunk);
        SelectedIndex = -1;
        SearchText = string.Empty;
    }

    private void SetIsWorking()
    {
        IsWorking = Reading || Searching;
    }

    public async Task RunDefinitionAsync(MenuDefinition definition)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        _definition = definition;
        IsVisible = true;
        DisplayItems.Clear();
        SearchText = string.Empty;
        IsHeaderVisible = definition.Header != null;
        Header = definition.Header;
        SelectedIndex = 0;
        
        if (definition.AsyncFunction != null)
        {
            var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions 
            { 
                SingleReader = true,
                SingleWriter = false
            });
            
            var writerTask = definition.AsyncFunction(channel.Writer, cancellationTokenSource.Token);
            await ReadFromSourceAsync(channel.Reader, cancellationTokenSource.Token);
            await writerTask;
        }
    }
    
    //public void StartRead()
    //{
    //    IsVisible = true;
    //    DisplayItems.Clear();
    //    Task.Run(() =>
    //    {
    //        var stream = Console.OpenStandardInput();
    //        using (var reader = new StreamReader(stream))
    //        {
    //            ReadFromSource(ReadLines(reader));
    //        }
    //    });
    //}

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    private CancellationTokenSource? _currentSearchCancellationTokenSource;

    private async Task ProcessLoop()
    {
        while (!_cancellation.Token.IsCancellationRequested)
        {
            await _restartSearchSignal.WaitAsync();
            if (_currentSearchCancellationTokenSource != null)
            {
                await _currentSearchCancellationTokenSource.CancelAsync();
            }
            _currentSearchCancellationTokenSource = new CancellationTokenSource();
            await Render(_currentSearchCancellationTokenSource.Token);
        }
    }
    
    private ThreadLocalData GetLocalResultFromPool()
    {
        if (_localResultsPool.TryTake(out var result))
        {
            return result;
        }

        throw new Exception("Number of outstanding thread locals exceeded");
    }
    
    private void ReturnLocalResultToPool(ThreadLocalData threadLocalData)
    {
        _localResultsPool.Add(threadLocalData);
    }
    
    private static readonly IComparer<Entry> EntryComparer = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Line.Length.CompareTo(y.Line.Length);
        if (lengthComparison != 0) return lengthComparison;

        return string.Compare(x.Line, y.Line, StringComparison.Ordinal);
    });

    private Task Render(CancellationToken ct)
    {
        var searchString = _searchText;
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, searchString, true);
        Searching = true;
        var globalList = new List<Entry>(MaxItems);

        var completeChunks = _chunks.Where(c => c.IsComplete).ToList();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxDegreeOfParallelism
        };

        var numberOfItemsWithScores = 0;
        
        Parallel.ForEach(completeChunks, parallelOptions, GetLocalResultFromPool,
            (chunk, _, localData) =>
            {
                ItemScoreResult[]? result = null;
                if (chunk.TryGetResultCache(searchString, ref result, out var size))
                {
                    for (var i = 0; i < size; i++)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return localData;
                        }
                        Interlocked.Increment(ref numberOfItemsWithScores);
                        var itemScoreResult = result![i];
                        ProcessNewScore(chunk.Items[itemScoreResult.Index], itemScoreResult.Score, localData);
                    }
                }
                else
                {
                    chunk.SetQueryStringNoReset(searchString);
                    var numberOfLocalItemsAdded = 0;
                    for (var i = 0; i < chunk.Items.Length; i++)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return localData;
                        }
                        var line = chunk.Items[i];
                        var score = FuzzySearcher.GetScore(line, pattern, localData.Slab);
                        if (score > _definition.MinScore)
                        {
                            Interlocked.Increment(ref numberOfItemsWithScores);
                            chunk.SetResultCacheItemNoReset(i, score, numberOfLocalItemsAdded);
                            ProcessNewScore(line, score, localData);
                            numberOfLocalItemsAdded++;
                        }
                    }
                }
                return localData;
            }, ReturnLocalResultToPool);

        NumberOfScoredItems = numberOfItemsWithScores;

        if (ct.IsCancellationRequested)
        {
            Console.WriteLine("Cancelled");
            return Task.CompletedTask;
        }

        foreach (var localData in _localResultsPool)
        {
            globalList.AddRange(localData.Entries);
            localData.Entries.Clear();
        }
        globalList.Sort(EntryComparer);
        var topEntries = globalList.Take(MaxItems).ToList();

        var previousIndex = SelectedIndex;
        DisplayItems.Clear();
        foreach (var item in topEntries)
        {
            var pos = FuzzySearcher.GetPositions(item.Line, pattern, _positionsSlab);
            _positionsSlab.Reset();
            DisplayItems.Add(new HighlightedText(item.Line, pos));
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
    
    private static void ProcessNewScore(string line, int score, ThreadLocalData localData)
    {
        if (line == null)
        {
            return;
        }

        var entry = new Entry(line, score);

        // Insert the entry in a sorted manner
        var list = localData.Entries;

        // Binary search to find the correct insertion point
        int index = list.BinarySearch(entry, EntryComparer);

        // BinarySearch returns a negative value for the insertion point
        if (index < 0) index = ~index;

        // Add the new entry at the correct position
        list.Insert(index, entry);

        // Keep the list size within the MaxItems limit
        if (list.Count > MaxItems)
        {
            list.RemoveAt(list.Count - 1); // Remove the lowest-priority item
        }
    }

    private async Task ReadFromSourceAsync(ChannelReader<string> channelReader, CancellationToken cancellationToken)
    {
        var numberOfItems = 0;
        NumberOfItems = 0;
        Reading = true;

        var currentChunk = _chunks.Last();

        await foreach (var line in channelReader.ReadAllAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                //NEED to cancel publisher also
                return;
            }
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
                if (_definition.QuitOnEscape)
                {
                    Environment.Exit(0);
                }
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
        if (_currentSearchCancellationTokenSource != null)
        {
            _currentSearchCancellationTokenSource.Cancel();
        }

        ShowResults = false;
        IsVisible = false;
        _chunks.Clear();
        _chunks.Add(new Chunk());
    }
}