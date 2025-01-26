using System.Collections.Concurrent;
using System.Threading.Channels;
using nfm.menu;
using nfzf;
using nfzf.FileSystem;
using Win32FromForms;

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
    private AsyncAutoResetEvent _restartSearchSignal;
    private readonly UnboundedChannelOptions _channelOptions;
    private Win32Window _window;
    public List<StringWithPos> Items { get; }

    public void SetWindow(Win32Window window)
    {
        _window = window;
    }
    
    public ViewModel()
    {
        for (var i = 0; i < _maxDegreeOfParallelism; i++)
        {
            _localResultsPool.Add(new ThreadLocalData(Slab.MakeDefault()));
        }
        _positionsSlab = Slab.MakeDefault();
        Items = new List<StringWithPos>(MaxItems);
        for (int i = 0; i < MaxItems; i++)
        {
            Items.Add(new StringWithPos(String.Empty, EmptyPos));
        }
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
        var completeChunks = _chunks.Where(c => c.IsComplete).ToList();
        var items = new List<String>();

        if (string.IsNullOrEmpty(_searchString))
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
                    if (fullFilePath != null)
                    {
                        Items[ctr] = new StringWithPos(fullFilePath, EmptyPos);
                        //DisplayItems[ctr].Set(fullFilePath, new List<int>(), item);
                    }

                    ctr++;
                    itemsAdded++;
                }

                if (itemsAdded >= MaxItems)
                {
                    break;
                }
            }

            Win32Window.SetListBoxItems();
            for (var i = ctr; i < MaxItems; i++)
            {
                Items[i] = new StringWithPos(string.Empty, EmptyPos);
            }

            var numberOfItemsWithScores = 0;
            return Task.CompletedTask;
        }
        

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism
        };

        var ct = CancellationToken.None;
        
        var globalList = new List<Entry>(MaxItems);
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, _searchString, true);
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
                    //if (score.Item2 > 10)
                    {
                        //Interlocked.Increment(ref numberOfItemsWithScores);
                        SortAction(
                            line,
                            score.Item1,
                            score.Item2,
                            chunkWithIndex.chunkNumber * Chunk.MaxSize + i,
                            localData.Entries,
                            EntryComparer);
                    }
                }
                return localData;
            }, ReturnLocalResultToPool);

        //NumberOfScoredItems = numberOfItemsWithScores;

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
                    Items[i] = new StringWithPos(fullFilePath, pos);
                }
            }
            else
            {
                Items[i] = new StringWithPos(string.Empty, EmptyPos);
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

        return Task.CompletedTask;

        // Searching = false;
        // ShowResults = DisplayItems.Count > 0;
        // NumberOfScoredItems = NumberOfItems;

        //_restartSearchSignal.Set();
        //return Task.CompletedTask;
        
        return Task.CompletedTask;
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
        //NumberOfItems = 0;
        //Reading = true;

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

                    //NumberOfItems = numberOfItems;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        //NumberOfItems = numberOfItems;
        //Reading = false;

        currentChunk.SetComplete();
        _restartSearchSignal.Set();
    }
    
    public void SetSearchString(string message)
    {
        _searchString = message;
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