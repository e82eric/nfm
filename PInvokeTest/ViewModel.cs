using System.Collections.Concurrent;
using System.Threading.Channels;
using nfm.menu;
using nfzf;
using nfzf.FileSystem;
using Win32FromForms;

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
    private static IList<int> EmptyPos = new List<int>();
    private class ThreadLocalData(Slab slab)
    {
        public List<Entry> Entries = new(MaxItems);
        public Slab Slab { get; } = slab;
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = value;
            Win32Window.SetListBoxItems();
        }
    }

    private MenuDefinition _definition;
    private CancellationTokenSource _currentDefinitionCancellationTokenSource;
    private Slab _positionsSlab;
    private const int MaxItems = 25;
    private readonly ConcurrentBag<ThreadLocalData> _localResultsPool = new();
    private readonly int _maxDegreeOfParallelism = Environment.ProcessorCount / 2;
    private IList<Chunk> _chunks;
    private string _searchString;
    private string _lastSearchString;
    private AsyncAutoResetEvent _restartSearchSignal;
    private readonly UnboundedChannelOptions _channelOptions;
    private Win32Window _window;
    public List<StringWithPos> Items { get; }
    private readonly object _snapshotLock = new();
    public bool Searching;
    public bool Reading;
    public int NumberOfItems;
    private int _searchStringVersion = 1;
    private int _lastSearchStringVersion = 0;
    public int NumberOfScoredItems = 0;

    public ViewModel()
    {
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
        _ = Task.Run(async () => await SearchLoop(), CancellationToken.None);
    }

    public bool FillSnapshot(Snapshot currentSnapshot, Snapshot snapshot)
    {
        var dirty = false;
        snapshot.Items.Clear();
        lock (_snapshotLock)
        {
            for (var i = 0; i < Math.Min(Items.Count, 16); i++)
            {
                if (currentSnapshot == null || i >= Items.Count - 1 || i >= currentSnapshot.Items.Count - 1)
                {
                    dirty = true;
                }
                else
                {
                    if (Items[i].Text != currentSnapshot.Items[i].Text)
                    {
                        dirty = true;
                    }
                }
                var item = Items[i];
                snapshot.Items.Add(item);
            }

            snapshot.NumberOfItems = NumberOfItems;
            snapshot.NumberOfScoredItems = NumberOfScoredItems;
            snapshot.IsWorking = Reading;
        }
        return dirty;
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

        //if (Items.Any() && previousIndex < 0)
        //{
        //    previousIndex = 0;
        //}
        //else if (previousIndex > 0 && previousIndex > Items.Count - 1)
        //{
        //    previousIndex = 0;
        //}

        ////Searching = false;
        ////ShowResults = DisplayItems.Count > 0;

        //if (previousIndex > -1 && Items.Count - 1 >= previousIndex)
        //{
        //    //SelectedIndex = previousIndex;
        //}

        Searching = false;
        return Task.CompletedTask;

        // Searching = false;
        // ShowResults = DisplayItems.Count > 0;
        // NumberOfScoredItems = NumberOfItems;

        //_restartSearchSignal.Set();
        //return Task.CompletedTask;
        
        //return Task.CompletedTask;
    }
    
    (int, int) ScoreFunc(object nodeObj, Pattern pattern, Slab slab)
    {
        Span<char> buf = stackalloc char[2048];
        var node = (FileSystemNode)nodeObj;
        var toScore = node.ToString(buf);
        var score = FuzzySearcher.GetScore(toScore, pattern, slab);
        return (toScore.Length, score);
    }
    
    private readonly IComparer<Entry> EntryComparer = Comparer<Entry>.Create((x, y) =>
    {
        int scoreComparison = y.Score.CompareTo(x.Score);
        if (scoreComparison != 0) return scoreComparison;

        int lengthComparison = x.Length.CompareTo(y.Length);
        return lengthComparison;
    });


    private int _selectedIndex;

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

    public async Task Run(FileWalker walker, string rootDirectory)
    {
        var channel = Channel.CreateUnbounded<object>(_channelOptions);
        var writerTask = walker.StartScanForDirectoriesAsync(
            [rootDirectory],
            channel.Writer,
            int.MaxValue,
            false,
            false,
            CancellationToken.None);
        await ReadFromSourceAsync(channel.Reader, CancellationToken.None);
        await writerTask;
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

    public Task ShowToast(string message, int duration = 3000)
    {
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