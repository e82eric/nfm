using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public readonly struct Entry(string line, int score, int index)
{
    public readonly string Line = line;
    public readonly int Score = score;
    public readonly int Index = index;
}
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<(KeyModifiers, Key), Func<string, Task>> _globalKeyBindings;

    
    private class ThreadLocalData(Slab slab)
    {
        public List<Entry> Entries { get; } = new(MaxItems);
        public Slab Slab { get; } = slab;
    }
    
    private readonly ConcurrentBag<ThreadLocalData> _localResultsPool = new();
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private int _selectedIndex;
    private bool _reading;
    private bool _searching;
    private int _numberOfItems;
    private bool _isWorking;
    private int _numberOfScoredItems;
    private const int MaxItems = 256 * 2;
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
    private CancellationTokenSource? _currentSearchCancellationTokenSource;
    private bool _hasPreview;
    private CancellationTokenSource? _currentDefinitionCancellationTokenSource;
    private string _toastMessage;
    private bool _isToastVisible;
    private readonly UnboundedChannelOptions _channelOptions;

    public string ToastMessage
    {
        get => _toastMessage;
        set
        {
            if (value == _toastMessage) return;
            _toastMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        set
        {
            if (value == _isToastVisible) return;
            _isToastVisible = value;
            OnPropertyChanged();
        }
    }

    public bool HasPreview
    {
        get => _hasPreview;
        set
        {
            if (value == _hasPreview) return;
            _hasPreview = value;
            OnPropertyChanged();
        }
    }

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
    
    public MainViewModel(Dictionary<(KeyModifiers, Key), Func<string, Task>> globalKeyBindings)
    {
        _channelOptions = new UnboundedChannelOptions 
        { 
            SingleReader = true,
            SingleWriter = false
        };
        _globalKeyBindings = globalKeyBindings;
        IsVisible = false;
        _positionsSlab = Slab.MakeDefault();
        _cancellation = new CancellationTokenSource();
        
        for (var i = 0; i < _maxDegreeOfParallelism; i++)
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
        _currentDefinitionCancellationTokenSource = new CancellationTokenSource();
        _definition = definition;
        HasPreview = _definition.HasPreview;
        IsVisible = true;
        DisplayItems.Clear();
        OnPropertyChanged(nameof(DisplayItems));
        SearchText = string.Empty;
        IsHeaderVisible = definition.Header != null;
        Header = definition.Header;
        SelectedIndex = 0;
        
        if (definition.AsyncFunction != null)
        {
            var channel = Channel.CreateUnbounded<string>(_channelOptions);
            
            var writerTask = definition.AsyncFunction(channel.Writer, _currentDefinitionCancellationTokenSource.Token);
            await ReadFromSourceAsync(channel.Reader, _currentDefinitionCancellationTokenSource.Token);
            await writerTask;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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
        var completeChunks = _chunks.Where(c => c.IsComplete).ToList();

        if (string.IsNullOrEmpty(_searchText))
        {
            DisplayItems.Clear();
            var itemsAdded = 0;
            foreach (var chunk in completeChunks)
            {
                foreach (var item in chunk.Items)
                {
                    if (itemsAdded >= MaxItems)
                    {
                        break;
                    }
                    DisplayItems.Add(new HighlightedText(item, new List<int>()));
                    itemsAdded++;
                }
                if (itemsAdded >= MaxItems)
                {
                    break;
                }
            }
            
            OnPropertyChanged(nameof(DisplayItems));

            Searching = false;
            ShowResults = DisplayItems.Count > 0;
            SelectedIndex = 0;

            return Task.CompletedTask;
        }
        
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, searchString, true);
        Searching = true;
        var globalList = new List<Entry>(MaxItems);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism
        };

        var numberOfItemsWithScores = 0;
        IComparer<Entry> comparer = _definition.Comparer ?? EntryComparer;
        
        Parallel.ForEach(completeChunks.Select((chunk, index) => (chunk, chunkNumber: index)), parallelOptions, 
            GetLocalResultFromPool, 
            (chunkWithIndex, _, localData) =>
            {
                for (var i = 0; i < chunkWithIndex.chunk.Size; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return localData;
                    }
                    var line = chunkWithIndex.chunk.Items[i];
                    var score = FuzzySearcher.GetScore(line, pattern, localData.Slab);
                    if (score > _definition.MinScore)
                    {
                        Interlocked.Increment(ref numberOfItemsWithScores);
                        ProcessNewScore(line, score, chunkWithIndex.chunkNumber * Chunk.MaxSize + i, localData, comparer);
                    }
                }
                return localData;
            }, ReturnLocalResultToPool);

        NumberOfScoredItems = numberOfItemsWithScores;

        if (ct.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        foreach (var localData in _localResultsPool)
        {
            globalList.AddRange(localData.Entries);
            localData.Entries.Clear();
        }

        globalList.Sort(comparer);
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
    
    private static void ProcessNewScore(string line, int score, int i, ThreadLocalData localData, IComparer<Entry> comparer)
    {
        if (line == null)
        {
            return;
        }

        var entry = new Entry(line, score, i);
        var list = localData.Entries;
        int index = list.BinarySearch(entry, comparer);
        
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

    public async Task HandleKey(Key eKey, KeyModifiers eKeyModifiers)
    {
        if (eKeyModifiers == KeyModifiers.Control)
        {
            if (_definition.KeyBindings.TryGetValue((eKeyModifiers, eKey), out var action))
            {
                await action(DisplayItems[SelectedIndex].Text);
            }
            else if (_globalKeyBindings.TryGetValue((eKeyModifiers, eKey), out var globalAction))
            {
                await globalAction(DisplayItems[SelectedIndex].Text);
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
                await Close();
                if (_definition.QuitOnEscape)
                {
                    Environment.Exit(0);
                }
                break;
            case Key.Enter:
                if (SelectedIndex >= 0 && SelectedIndex < DisplayItems.Count)
                {
                    await _definition.ResultHandler.HandleAsync(DisplayItems[SelectedIndex].Text, this);
                }
                break;
        }
    }
    public async Task ShowToast(string message, int duration = 3000)
    {
        ToastMessage = message;
        IsToastVisible = true;

        await Task.Delay(duration);

        IsToastVisible = false;
    }
    
    public async Task Clear()
    {
        if (_currentSearchCancellationTokenSource is { Token.IsCancellationRequested: false })
        {
            await _currentSearchCancellationTokenSource.CancelAsync();
        }
        if (_currentDefinitionCancellationTokenSource is { IsCancellationRequested: false })
        {
            await _currentDefinitionCancellationTokenSource.CancelAsync();
        }
                
        DisplayItems.Clear();
        OnPropertyChanged(nameof(DisplayItems));
        ShowResults = false;
        _chunks.Clear();
        _chunks.Add(new Chunk());
    }

    public async Task Close()
    {
        IsVisible = false;
        await Clear();
    }
}