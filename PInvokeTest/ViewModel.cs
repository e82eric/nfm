using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.Versioning;
using System.Threading.Channels;
using nfm.menu;
using nfzf;
using Win32FromForms;

internal enum PreviewType
{
    Text,
    Image
}

class Snapshot
{
    public Snapshot()
    {
        Items = new List<StringWithPos>(16);
    }
    public IList<StringWithPos> Items { get; private set; }
    public int NumberOfItems { get; set; }
    public int NumberOfScoredItems { get; set; }
    public bool IsWorking { get; set; }
    public int SelectedIndex { get; set; }
}

[SupportedOSPlatform("windows")]
class ViewModel : IMainViewModel, IPreviewRenderer
{
    private class ThreadLocalData(Slab slab)
    {
        public List<Entry> Entries = new(MaxItems);
        public Slab Slab { get; } = slab;
    }

    private const int MaxItems = 1000;
    private static readonly IList<int> EmptyPos = new List<int>();
    
    private MenuDefinition? _definition;
    private CancellationTokenSource? _currentDefinitionCancellationTokenSource;

    private readonly Slab _positionsSlab;
    private readonly ConcurrentBag<ThreadLocalData> _localResultsPool = new();
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount / 2;
    private readonly IList<Chunk> _chunks;
    private string _searchString;
    private readonly AsyncAutoResetEvent _restartSearchSignal;
    private readonly AsyncAutoResetEvent _previewSignal;
    private readonly UnboundedChannelOptions _channelOptions;
    private readonly Lock _snapshotLock = new();
    public bool Searching;
    public bool Reading;
    public int NumberOfItems;
    private int _searchStringVersion = 1;
    private int _lastSearchStringVersion = 0;
    public int NumberOfScoredItems = 0;
    private int _previewHeight;
    private List<StringWithPos> Items { get; }
    private string _lastPreviewPath { get; set; }
    private bool _showPreview { get; set; }
    private Viewport _viewport;
    
    public Dictionary<(ModifierKeys, int), Func<object, IMainViewModel, Task>> GlobalKeyBindings { get; } = new();

    public ViewModel()
    {
        _searchString = string.Empty;
        for (var i = 0; i < _maxDegreeOfParallelism; i++)
        {
            _localResultsPool.Add(new ThreadLocalData(Slab.MakeDefault()));
        }
        _positionsSlab = Slab.MakeDefault();
        Items = new List<StringWithPos>(MaxItems);
        _channelOptions = new UnboundedChannelOptions 
        { 
            SingleReader = true,
            SingleWriter = false
        };
        _chunks = new List<Chunk>();
        _chunks.Add(new Chunk());
        _restartSearchSignal = new AsyncAutoResetEvent();
        _previewSignal = new AsyncAutoResetEvent();
        
        _ = Task.Run(async () => await SearchLoop(), CancellationToken.None);
        _ = Task.Run(async () => await PreviewLoop(), CancellationToken.None);
    }

    public void FillSnapshot(Snapshot snapshot, int maxItems)
    {
        snapshot.Items.Clear();
        lock (_snapshotLock)
        {
            var numberOfRows = _viewport.EndRow - _viewport.StartRow;
            for (var i = 0; i < numberOfRows; i++)
            {
                var item = Items[i + _viewport.StartRow];
                snapshot.Items.Add(item);
            }

            snapshot.NumberOfItems = NumberOfItems;
            snapshot.NumberOfScoredItems = NumberOfScoredItems;
            snapshot.IsWorking = Reading;
            snapshot.SelectedIndex = _viewport.ViewportSelectedIndex;
        }
    }

    private Task RunPreview()
    {
        if (_showPreview)
        {
            string path;
            lock (_snapshotLock)
            {
                if (Items.Count <= _viewport.SelectedIndex || _viewport.SelectedIndex < 0)
                {
                    return Task.CompletedTask;
                }
                path = Items[_viewport.SelectedIndex].Text;
            }
            if (path == _lastPreviewPath)
            {
                return Task.CompletedTask;
            }
        
            _definition.PreviewHandler?.Handle(this, path, _previewHeight, CancellationToken.None);
            _lastPreviewPath = path;
        }

        return Task.CompletedTask;
    }

    private async Task SearchLoop()
    {
        while (true)
        {
            var delay = Task.Delay(TimeSpan.FromMilliseconds(250));
            var searchSignalTask = _restartSearchSignal.WaitAsync();
            await Task.WhenAny(delay, searchSignalTask);
            await Search();
        }
    }

    private async Task PreviewLoop()
    {
        while (true)
        {
            await _previewSignal.WaitAsync();
            await RunPreview();
            await Task.Delay(250);
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

    private Task Search()
    {
        if (_definition == null)
        {
            return Task.CompletedTask;
        }
        
        if (!Reading && _searchStringVersion == _lastSearchStringVersion)
        {
            return Task.CompletedTask;
        }

        Searching = true;
        _lastSearchStringVersion = _searchStringVersion;
        var completeChunks = _chunks.Where(c => c.IsComplete).ToList();

        if (string.IsNullOrEmpty(_searchString))
        {
            lock (_snapshotLock)
            {
                Items.Clear();
                var itemsAdded = 0;
                foreach (var chunk in completeChunks)
                {
                    foreach (var item in chunk.Items)
                    {
                        if (item == null || itemsAdded >= MaxItems)
                        {
                            break;
                        }

                        var fullFilePath = item.ToString();
                        if (fullFilePath != null)
                        {
                            Items.Add(new StringWithPos(fullFilePath, EmptyPos));
                        }

                        itemsAdded++;
                    }

                    if (itemsAdded >= MaxItems)
                    {
                        break;
                    }
                }
                _viewport.SetTotalRows(Items.Count);
            }

            NumberOfScoredItems = NumberOfItems;
            Searching = false;
            Win32Window.SetListBoxItems();
            _previewSignal.Set();
            return Task.CompletedTask;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism
        };

        var ct = CancellationToken.None;
        
        var globalList = new List<Entry>(MaxItems);
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, _searchString, true);
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

        lock (_snapshotLock)
        {
            Items.Clear();
            for (int i = 0; i < MaxItems; i++)
            {
                if (i < topEntries.Count)
                {
                    var item = topEntries[i];
                    var fullFilePath = item.Item.ToString();
                    var pos = FuzzySearcher.GetPositions(fullFilePath, pattern, _positionsSlab);
                    _positionsSlab.Reset();
                    if (fullFilePath != null)
                    {
                        Items.Add(new StringWithPos(fullFilePath, pos));
                    }
                }
            }

            _viewport.SetTotalRows(Items.Count);
        }

        Win32Window.SetListBoxItems();
        _previewSignal.Set();

        Searching = false;
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
    
    public async Task RunDefinitionAsync(MenuDefinition definition)
    {
        _definition = definition;
        _currentDefinitionCancellationTokenSource = new CancellationTokenSource();
        if (_definition.Header != null)
        {
            Win32Window.SetHeader(_definition.Header);
        }
        if (definition.AsyncFunction != null)
        {
            var channel = Channel.CreateUnbounded<object>(_channelOptions);
            var writerTask = definition.AsyncFunction(channel.Writer, _currentDefinitionCancellationTokenSource.Token);
            await ReadFromSourceAsync(channel.Reader, _currentDefinitionCancellationTokenSource.Token);
            await writerTask;
        }
    }
    
    private async Task ReadFromSourceAsync(ChannelReader<object> channelReader, CancellationToken cancellationToken)
    {
        await ReadFromSourceAsync(channelReader.ReadAllAsync(cancellationToken), cancellationToken);
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
    
    public void SetSearchString(string message)
    {
        _searchString = message;
        _searchStringVersion++;
        _restartSearchSignal.Set();
    }
    
    public async Task HandleKeyUp(int eKey, ModifierKeys eKeyModifiers)
    {
        if (eKeyModifiers == ModifierKeys.LCtl)
        {
            if (eKey == VirtualKeyCodes.VK_E)
            {
                if (_definition?.EditAction != null)
                {
                    //EditDialogOpen = true;
                }
            }
            else if (_definition != null && _definition.KeyBindings.TryGetValue((eKeyModifiers, eKey), out var action))
            {
                var highlightedText = Items[_viewport.SelectedIndex];
                if (highlightedText != null && highlightedText.Text != null)
                {
                    await action(highlightedText.Text);
                }
            }
            else if (GlobalKeyBindings.TryGetValue((eKeyModifiers, eKey), out var globalAction))
            {
                StringWithPos highlightedText;
                lock (_snapshotLock)
                {
                    highlightedText = Items[_viewport.SelectedIndex];
                }
                if (highlightedText != null && highlightedText.Text != null)
                {
                    await globalAction(highlightedText.Text, this);
                }
            }
        }
    }

    public Task ShowToast(string message, int duration = 3000)
    {
        Win32Window.ShowToast(message, duration);
        return Task.CompletedTask;
    }

    public Task Clear()
    {
        return Task.CompletedTask;
    }

    public Task Close()
    {
        return Task.CompletedTask;
    }

    public void TogglePreview()
    {
        _showPreview = !_showPreview;
        _previewSignal.Set();
        Win32Window.TogglePreview(_showPreview); 
    }

    public void RenderImage(MemoryStream memoryStream)
    {
        var bitmap = new Bitmap(memoryStream);
        Win32Window.ShowImagePreview(bitmap);
    }

    public void RenderText(List<string> lines, string fileExtension)
    {
        Win32Window.SetPreviewLines(lines);
    }

    public void RenderError(string errorInfo)
    {
        Win32Window.SetPreviewLines([errorInfo]);
    }

    public void SetPreviewHeight(int height)
    {
        _previewHeight = height;
    }

    public void SetNumberOfRows(int rows)
    {
        _viewport = new Viewport(rows);
    }

    public void SelectNext()
    {
        _viewport.SelectNext();
        Win32Window.SetListBoxItems();
        _previewSignal.Set();
    }
    
    public void SelectPageDown()
    {
        _viewport.SelectHalfPageDown();
        Win32Window.SetListBoxItems();
        _previewSignal.Set();
    }
    
    public void SelectPageUp()
    {
        _viewport.SelectHalfPageUp();
        Win32Window.SetListBoxItems();
        _previewSignal.Set();
    }

    public void SelectPrevious()
    {
        _viewport.SelectPrevious();
        Win32Window.SetListBoxItems();
        _previewSignal.Set();
    }

    public async Task OnReturn()
    {
        await _definition.ResultHandler.HandleAsync(Items[_viewport.SelectedIndex].Text);
    }
    
    public void OnEscape()
    {
        if (_definition.QuitOnEscape)
        {
            Environment.Exit(0);
        }
    }
}