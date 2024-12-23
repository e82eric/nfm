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
using Avalonia.Media.Imaging;
using nfzf;

namespace nfm.menu;

public class MainViewModel : IPreviewRenderer, INotifyPropertyChanged, IMainViewModel
{
    public Dictionary<(KeyModifiers, Key), Func<object, MainViewModel, Task>> GlobalKeyBindings { get; }

    private class ThreadLocalData(Slab slab)
    {
        public List<Entry> Entries { get; } = new(MaxItems);
        public Slab Slab { get; } = slab;
    }
    
    private readonly ConcurrentBag<ThreadLocalData> _localResultsPool = new();
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount / 2;
    private int _selectedIndex;
    private bool _reading;
    private bool _searching;
    private int _numberOfItems;
    private bool _isWorking;
    private int _numberOfScoredItems;
    private const int MaxItems = 250;
    private readonly List<Chunk> _chunks = new();
    private readonly CancellationTokenSource _cancellation;
    private string _searchText;
    private readonly AsyncAutoResetEvent _restartSearchSignal = new();
    private readonly AsyncAutoResetEvent _restartPreviewSignal = new();
    private bool _showResults;
    private ObservableCollection<HighlightedText> _displayItems = new();
    private readonly Slab _positionsSlab;
    private bool _isVisible;
    private string? _header;
    private bool _isHeaderVisible;
    private MenuDefinition _definition;
    private CancellationTokenSource? _currentSearchCancellationTokenSource;
    private CancellationTokenSource? _currentPreviewCancellationTokenSource;
    private bool _hasPreview;
    private CancellationTokenSource? _currentDefinitionCancellationTokenSource;
    private string _toastMessage;
    private bool _isToastVisible;
    private readonly UnboundedChannelOptions _channelOptions;
    private string _previewText;
    private Bitmap _previewImage;
    private string _selectedText;

    public bool EditDialogOpen
    {
        get => _editDialogOpen;
        set
        {
            if (value == _editDialogOpen) return;
            _editDialogOpen = value;
            OnPropertyChanged();
        }
    }

    public string PreviewExtension { get; set; }

    public Bitmap PreviewImage
    {
        get => _previewImage;
        set
        {
            //if (Equals(value, _previewImage)) return;
            _previewImage = value;
            _previewText = string.Empty;
            OnPropertyChanged();
        }
    }

    public string PreviewText
    {
        get => _previewText;
        set
        {
            if (value == _previewText) return;
            _previewText = value;
            OnPropertyChanged();
        }
    }

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
            if (_selectedIndex >= 0 && _selectedIndex < DisplayItems.Count)
            {
                SelectedText = DisplayItems[_selectedIndex].Text;
            }
        }
    }

    private string SelectedText
    {
        get => _selectedText;
        set
        {
            if(value == _selectedText) return;
            _selectedText = value;
            _restartPreviewSignal.Set();
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
    
    public MainViewModel()
    {
        GlobalKeyBindings = new Dictionary<(KeyModifiers, Key), Func<object, MainViewModel, Task>>();
        _channelOptions = new UnboundedChannelOptions 
        { 
            SingleReader = true,
            SingleWriter = false
        };
        IsVisible = false;
        _positionsSlab = Slab.MakeDefault();
        _cancellation = new CancellationTokenSource();
        
        for (var i = 0; i < _maxDegreeOfParallelism; i++)
        {
            _localResultsPool.Add(new ThreadLocalData(Slab.MakeDefault()));
        }

        //Start the processing loop.  need to handle the task better.
        _ = Task.Run(ProcessLoop);
        _ = Task.Run(PreviewLoop);

        var firstChunk = new Chunk();
        _chunks.Add(firstChunk);
        SelectedIndex = -1;
        SearchText = string.Empty;

        for (int i = 0; i < MaxItems; i++)
        {
            DisplayItems.Add(new HighlightedText("", new List<int>()));
        }
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
        SearchText = definition.SearchString;
        IsVisible = true;
        IsHeaderVisible = definition.Header != null;
        Header = definition.Header;
        SelectedIndex = 0;
        
        if (definition.AsyncFunction != null)
        {
            var channel = Channel.CreateUnbounded<object>(_channelOptions);
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

    private CancellationTokenSource _debounceCts = new();
    private bool _editDialogOpen;

    private async Task ProcessLoop()
    {
        const int debounceDelayMilliseconds = 0;

        while (!_cancellation.Token.IsCancellationRequested)
        {
            await _restartSearchSignal.WaitAsync();

            await _debounceCts.CancelAsync();
            _debounceCts.Dispose();

            _debounceCts = new CancellationTokenSource();
            var debounceToken = _debounceCts.Token;

            try
            {
                await Task.Delay(debounceDelayMilliseconds, debounceToken);
            }
            catch (TaskCanceledException)
            {
                continue;
            }

            if (_currentSearchCancellationTokenSource != null)
            {
                await _currentSearchCancellationTokenSource.CancelAsync();
            }
            _currentSearchCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Render(_currentSearchCancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                //Console.WriteLine(e);
            }
        }
    }

    private async Task PreviewLoop()
    {
        const int debounceDelay = 150;
        CancellationTokenSource debounceCts = null;

        var ctr = 0;
        while (!_cancellation.Token.IsCancellationRequested)
        {
            try
            {
                await _restartPreviewSignal.WaitAsync();
                ctr++;

                if (_currentPreviewCancellationTokenSource != null && !_currentPreviewCancellationTokenSource.Token.IsCancellationRequested)
                {
                    await _currentPreviewCancellationTokenSource.CancelAsync();
                }

                if (debounceCts != null)
                {
                    await debounceCts.CancelAsync();
                }

                debounceCts = new CancellationTokenSource();
                CancellationToken debounceToken = debounceCts.Token;

                try
                {
                    await Task.Delay(debounceDelay, debounceToken);
                }
                catch (TaskCanceledException)
                {
                    //continue;
                }

                _currentPreviewCancellationTokenSource = new CancellationTokenSource();
                try
                {
                    await RenderPreview(_currentPreviewCancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    PreviewText = e.Message;
                }
            }
            catch (Exception e)
            {
                RenderError(e.Message);
            }
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

    private async Task RenderPreview(CancellationToken ct)
    {
        if (DisplayItems.Count > SelectedIndex && HasPreview)
        {
            var selected = DisplayItems[SelectedIndex];
            if (selected != null)
            {
                var backing = selected.BackingObj;
                if (_definition.PreviewHandler != null && backing != null)
                {
                    await _definition.PreviewHandler.Handle(this, backing, ct);
                }
            }
        }
    }

    private Task Render(CancellationToken ct)
    {
        var searchString = _searchText;
        var completeChunks = _chunks.Where(c => c.IsComplete).ToList();

        if (string.IsNullOrEmpty(_searchText))
        {
            var itemsAdded = 0;
            var ctr = 0;
            foreach (var chunk in completeChunks)
            {
                foreach (var item in chunk.Items)
                {
                    if (item == null || itemsAdded >= MaxItems)
                    {
                        break;
                    }
                    var fullFilePath = item.ToString();
                    DisplayItems[ctr].Set(fullFilePath, new List<int>(), item);
                    ctr++;
                    itemsAdded++;
                }
                if (itemsAdded >= MaxItems)
                {
                    break;
                }
            }
            for (var i = ctr; i < MaxItems; i++)
            {
                DisplayItems[i].Set(string.Empty, new List<int>(), null);
            }

            Searching = false;
            ShowResults = DisplayItems.Count > 0;
            NumberOfScoredItems = NumberOfItems;

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
                    var score = _definition.ScoreFunc(line, pattern, localData.Slab);
                    if (score.Item2 > _definition.MinScore)
                    {
                        Interlocked.Increment(ref numberOfItemsWithScores);
                        SortAction(
                            line,
                            score.Item1,
                            score.Item2,
                            chunkWithIndex.chunkNumber * Chunk.MaxSize + i,
                            localData.Entries,
                            _definition.Comparer);
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

        globalList.Sort(_definition.FinalComparer);
        var topEntries = globalList.Take(MaxItems).ToList();

        var previousIndex = SelectedIndex;
        for (int i = 0; i < MaxItems; i++)
        {
            if (i < topEntries.Count)
            {
                var item = topEntries[i];
                var fullFilePath = item.Item.ToString();
                var pos = FuzzySearcher.GetPositions(fullFilePath, pattern, _positionsSlab);
                _positionsSlab.Reset();
                DisplayItems[i].Set(fullFilePath, pos, item.Item);
            }
            else
            {
                DisplayItems[i].Set(string.Empty, new List<int>(), null);
            }
        }

        if (DisplayItems.Any() && previousIndex < 0)
        {
            previousIndex = 0;
        }
        else if (previousIndex > 0 && previousIndex > DisplayItems.Count - 1)
        {
            previousIndex = 0;
        }

        Searching = false;
        ShowResults = DisplayItems.Count > 0;

        if (previousIndex > -1 && DisplayItems.Count - 1 >= previousIndex)
        {
            SelectedIndex = previousIndex;
        }

        return Task.CompletedTask;
    }
    
    void SortAction(object node, int length, int score, int i, List<Entry> results, IComparer<Entry>? comparer)
    {
        var entry = new Entry(node, length, score, i);
        var list = results;
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
    
    private async Task ReadFromSourceAsync(IAsyncEnumerable<object> source, CancellationToken cancellationToken)
    {
        var numberOfItems = 0;
        NumberOfItems = 0;
        Reading = true;

        var currentChunk = _chunks.Last();

        try
        {
            await foreach (var line in source.WithCancellation(cancellationToken))
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
        }
        catch (OperationCanceledException)
        {
        }

        NumberOfItems = numberOfItems;
        Reading = false;

        currentChunk.SetComplete();
        _restartSearchSignal.Set();
    }
    
    private async Task ReadFromSourceAsync(ChannelReader<object> channelReader, CancellationToken cancellationToken)
    {
        await ReadFromSourceAsync(channelReader.ReadAllAsync(cancellationToken), cancellationToken);
    }

    public async Task HandleKeyUp(Key eKey, KeyModifiers eKeyModifiers)
    {
        switch (eKey)
        {
            case Key.End:
                if (eKeyModifiers == KeyModifiers.Control)
                {
                    SelectedIndex = NumberOfScoredItems - 1;
                }
                break;
            case Key.Home:
                if (eKeyModifiers == KeyModifiers.Control)
                {
                    SelectedIndex = 0;
                }
                break;
        }
        
        if (eKeyModifiers == KeyModifiers.Control)
        {
            if (eKey == Key.E)
            {
                if (_definition.EditAction != null)
                {
                    EditDialogOpen = true;
                }
            }
            else if (_definition.KeyBindings.TryGetValue((eKeyModifiers, eKey), out var action))
            {
                var highlightedText = DisplayItems[SelectedIndex];
                if (highlightedText != null)
                {
                    await action(highlightedText.BackingObj);
                }
            }
            else if (GlobalKeyBindings.TryGetValue((eKeyModifiers, eKey), out var globalAction))
            {
                var highlightedText = DisplayItems[SelectedIndex];
                if (highlightedText != null)
                {
                    await globalAction(highlightedText.BackingObj, this);
                }
            }
        }
    }

    public async Task HandleKey(Key eKey, KeyModifiers eKeyModifiers)
    {
        switch (eKey)
        {
            case Key.PageUp:
                SelectedIndex = Math.Max(0, SelectedIndex - 7);
                break;
            case Key.PageDown:
                SelectedIndex = Math.Min(NumberOfScoredItems - 1, SelectedIndex + 7);
                break;
            case Key.Down:
                var nextIndex = Math.Min(NumberOfScoredItems - 1, SelectedIndex + 1);
                SelectedIndex = nextIndex;
                break;
            case Key.Up:
                var previousIndex = Math.Max(0, SelectedIndex - 1);
                SelectedIndex = previousIndex;
                break;
            case Key.D:
                if (eKeyModifiers == KeyModifiers.Control)
                {
                    SelectedIndex = Math.Min(NumberOfScoredItems - 1, SelectedIndex + 7);
                }
                break;
            case Key.U:
                if (eKeyModifiers == KeyModifiers.Control)
                {
                    SelectedIndex = Math.Max(0, SelectedIndex - 7);
                }
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
                    var highlightedText = DisplayItems[SelectedIndex] as HighlightedText;
                    if (highlightedText != null)
                    {
                        await _definition.ResultHandler.HandleAsync(highlightedText.BackingObj);
                    }
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
        
        ShowResults = false;
        _chunks.Clear();
        _chunks.Add(new Chunk());
    }

    public async Task Close()
    {
        IsVisible = false;
        await Clear();
    }

    public void Closed()
    {
        if (_definition.OnClosed != null)
        {
            _definition.OnClosed();
        }
    }

    public void TogglePreview()
    {
        if (_definition.PreviewHandler != null)
        {
            HasPreview = !HasPreview;
            _restartPreviewSignal.Set();
        }
    }

    public void RenderImage(Bitmap bitmap)
    {
        PreviewImage = bitmap;
    }

    public void RenderText(string info, string fileExtension)
    {
        PreviewText = info;
        PreviewExtension = fileExtension;
    }

    public void RenderError(string errorInfo)
    {
        PreviewText = errorInfo;
        PreviewExtension = ".txt";
    }

    public async Task RunEditAction(object item, string newValue)
    {
        if (_definition.EditAction != null)
        {
            var result = await _definition.EditAction(item, newValue);
            if (result.Success)
            {
                var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, SearchText, true);
                var pos = FuzzySearcher.GetPositions(newValue, pattern, _positionsSlab);
                DisplayItems[SelectedIndex] = new HighlightedText(newValue, pos, item);
                SelectedIndex = SelectedIndex;
            }
            else
            {
                await ShowToast(result.ErrorMessage);
            }
        }
    }
}