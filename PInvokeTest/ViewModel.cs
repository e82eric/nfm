using System.Collections.Concurrent;
using System.Threading.Channels;
using nfm.menu;
using nfzf;
using Win32FromForms;

class TextPreviewToken
{
    public string Text { get; set; }
    public int BGRColor { get; set; }
}

class TextPreviewLine
{
    public IList<TextPreviewToken> Tokens { get; } = new List<TextPreviewToken>();
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
}

class ViewModel : IMainViewModel
{
    private class ThreadLocalData(Slab slab)
    {
        public List<Entry> Entries = new(MaxItems);
        public Slab Slab { get; } = slab;
    }

    private const int MaxItems = 25;
    private static readonly IList<int> EmptyPos = new List<int>();
    
    private MenuDefinition? _definition;
    private CancellationTokenSource? _currentDefinitionCancellationTokenSource;

    private int _selectedIndex;
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
    private List<StringWithPos> Items { get; }
    private string _lastPreviewPath { get; set; }
    
    public Dictionary<(ModifierKeys, int), Func<object, IMainViewModel, Task>> GlobalKeyBindings { get; } = new();
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            Win32Window.SetListBoxItems();
            _previewSignal.Set();
        }
    }

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

    public void FillSnapshot(Snapshot snapshot)
    {
        snapshot.Items.Clear();
        lock (_snapshotLock)
        {
            for (var i = 0; i < Math.Min(Items.Count, 16); i++)
            {
                var item = Items[i];
                snapshot.Items.Add(item);
            }

            snapshot.NumberOfItems = NumberOfItems;
            snapshot.NumberOfScoredItems = NumberOfScoredItems;
            snapshot.IsWorking = Reading;
        }
    }

    private async Task RunPreview()
    {
        var result = new List<string>(18);
        var path = string.Empty;
        lock (_snapshotLock)
        {
            if (Items.Count < SelectedIndex || SelectedIndex < 0)
            {
                return;
            }
            path = Items[SelectedIndex].Text;
        }
        if (path == _lastPreviewPath)
        {
            return;
        }
        var info = new FileInfo(path);
        
        var textColor = 0x008499a8;
        if (!info.Exists || (info.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
        {
            var dirInfo = new DirectoryInfo(path);
            var infos = new []
            {
                $"Directory: {dirInfo.Name}",
                $"Path: {dirInfo.FullName}",
                $"Created: {dirInfo.CreationTime}",
                $"Last Modified: {dirInfo.LastWriteTime}",
                $"Attributes: {dirInfo.Attributes}"
            };
            foreach (var str in infos)
            {
                result.Add(str);
            }
            foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos())
            {
                result.Add($"  {fileSystemInfo.Name}\n");
            }
            
            Win32Window.SetPreviewLines(result);
            return;
        }

        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using (var reader = new StreamReader(stream))
            {
                //if (ct.IsCancellationRequested)
                //{
                //    return (false, null);
                //}
                    
                var buffer = new char[1024];
                int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);

                // Check for binary content in the first 1024 characters
                for (int i = 0; i < charsRead; i++)
                {
                    if (buffer[i] == '\0' ||
                        (buffer[i] < 32 && buffer[i] != '\t' && buffer[i] != '\n' && buffer[i] != '\r'))
                    {
                        result.Add($"File appears to be binary: {path}");
                        Win32Window.SetPreviewLines(result);
                        return;
                    }
                }
            }
        }

        var maxLines = 40;
        var lines = new List<string>();
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream))
        {
            // If the file is determined to be text, read the first `maxLines`
            stream.Position = 0; // Reset to the beginning for reading lines
            while (!reader.EndOfStream && lines.Count < maxLines)
            {
                //if (ct.IsCancellationRequested)
                //{
                //    return (false, null);
                //}
                var line = await reader.ReadLineAsync();
                if (line != null)
                {
                    lines.Add(line);
                }
            }
        }
        
        _lastPreviewPath = path;
        
        foreach (var line in lines)
        {
            result.Add(line);
        }
        Win32Window.SetPreviewLines(result);
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
                        if (fullFilePath != null)
                        {
                            Items.Add(new StringWithPos(fullFilePath, EmptyPos));
                        }

                        ctr++;
                        itemsAdded++;
                    }

                    if (itemsAdded >= MaxItems)
                    {
                        break;
                    }
                }
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

        //var previousIndex = SelectedIndex;
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
                var highlightedText = Items[SelectedIndex];
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
                    highlightedText = Items[SelectedIndex];
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
}