using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.Versioning;
using System.Threading.Channels;
using nfm.Ui.Core;
using nfzf;

namespace nfm.Win32Ui;

internal enum PreviewType
{
    Text,
    Image
}

public class Snapshot
{
    public Snapshot()
    {
        Items = new List<(object Item, TerminalEscapedLine Text)>(16);
    }
    public IList<(object Item, TerminalEscapedLine Text)> Items { get; private set; }
    public int NumberOfItems { get; set; }
    public int NumberOfScoredItems { get; set; }
    public bool IsWorking { get; set; }
    public int SelectedIndex { get; set; }
    public bool ShowGap { get; set; }
    public int StartLinesToClip { get; set; }
    public bool WrapLines { get; set; }
}

[SupportedOSPlatform("windows")]
public class ViewModel : IMainViewModel, IPreviewRenderer
{
    public IList<ColumnFilter> SortFilters { get; private set; }
    
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
    private readonly Lock _searchStringLock = new();
    public bool Searching;
    public bool Reading;
    public int NumberOfItems;
    public int NumberOfScoredItems = 0;
    private int _previewHeight;
    private List<(object Obj, TerminalEscapedLine Text)> Items { get; }
    private string _lastPreviewPath { get; set; } = string.Empty;
    private bool _showPreview { get; set; }
    private Viewport? _viewport;
    private PreviewViewport? _previewViewport;
    private Win32Window? _view;
    private int _searchVersion = 0;
    private int _lastSearchVersion = -1;
    private string _previewVimState = string.Empty;
    private DateTime _previewVimLastKeyPressTime;
    private readonly TimeSpan _previewVimTimeout = TimeSpan.FromSeconds(1);
    
    public Dictionary<(ModifierKeys, int), Func<object, IMainViewModel, Task>> GlobalKeyBindings { get; } = new();
    
    private Win32Window View => _view ! ?? throw new InvalidOperationException("View has not been set.");
    private Viewport ViewPort => _viewport ! ?? throw new InvalidOperationException("Viewport has not been set.");
    public PreviewViewport PreviewViewPort => _previewViewport ! ?? throw new InvalidOperationException("PreviewViewport has not been set.");
    
    public void SetView(Win32Window view)
    {
        _view = view;
    }

    public ViewModel()
    {
        _searchString = string.Empty;
        for (var i = 0; i < _maxDegreeOfParallelism; i++)
        {
            _localResultsPool.Add(new ThreadLocalData(Slab.MakeDefault()));
        }
        _positionsSlab = Slab.MakeDefault();
        Items = new List<(object, TerminalEscapedLine)>(MaxItems);
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
        if (_definition is null)
        {
            return;
        }
        
        snapshot.Items.Clear();
        if (!Items.Any())
        {
            return;
        }
        lock (_snapshotLock)
        {
            for (var i = ViewPort.StartRow; i <= ViewPort.EndRow; i++)
            {
                var item = Items[i];
                snapshot.Items.Add(item);
            }

            snapshot.NumberOfItems = NumberOfItems;
            snapshot.NumberOfScoredItems = NumberOfScoredItems;
            snapshot.IsWorking = Reading;
            snapshot.SelectedIndex = ViewPort.ViewportSelectedIndex;
            snapshot.ShowGap = _definition.ShowGap;
            snapshot.StartLinesToClip = ViewPort.StartLinesToClip;
            snapshot.WrapLines = _definition.Wrap;
        }
    }

    private Task RunPreview()
    {
        if (_definition != null &&_showPreview)
        {
            var item = View.GetSelectedItem();
            if (item == null)
            {
                return Task.CompletedTask;
            }
            var path = item.Value.Item.ToString();
            if (path == null ||path == _lastPreviewPath)
            {
                return Task.CompletedTask;
            }
        
            _definition.PreviewHandler?.Handle(this, item.Value.Item, _previewHeight, CancellationToken.None);
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
            //Searching is all cpu, need to make sure this doesn't get scheduled on the same thread as reading from the source
            await Task.Run(Search);
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

    private void Search()
    {
        if (_definition == null || _lastSearchVersion == _searchVersion)
        {
            return;
        }

        var currentSearchVersion = _searchVersion;
        Searching = true;
        var completeChunks = _chunks.ToList();

        String currentSearchString;
        lock (_searchStringLock)
        {
            currentSearchString = _searchString;
        }

        var globalList = new List<Entry>(MaxItems);
        SortFilters = ColumnFilterParser.ParseSortColumnFilters(currentSearchString);
        var parsedSearchString = _definition.PreParseFunc?.Invoke(currentSearchString) ?? currentSearchString;
        if (string.IsNullOrEmpty(parsedSearchString))
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

                        if (_definition.PreFilter != null && !_definition.PreFilter.Invoke(item, currentSearchString))
                        {
                            continue;
                        }
                
                        globalList.Add(new Entry(item, item.ToString().Length, 0,  itemsAdded));
                
                        itemsAdded++;
                    }

                    if (_definition.PostProcess != null)
                    {
                        _definition.PostProcess(currentSearchString, globalList);
                    }

                    foreach (var entry in globalList)
                    {
                        var item = entry.Item;
                        if (item is TerminalEscapedLine escapedLine)
                        {
                            escapedLine.SetPos(EmptyPos);
                            Items.Add((item, escapedLine));
                        }
                        else
                        {
                            var fullFilePath = item.ToString();
                            if (fullFilePath != null)
                            {
                                var segment = new TextSegment { State = new AnsiState(), Text = fullFilePath };
                                var segments = new List<TextSegment> { segment };
                                var line = new EscapedLine(segments);
                                var terminalEscapedLine = new TerminalEscapedLine();
                                terminalEscapedLine.Lines.Add(line);
                                Items.Add((item, terminalEscapedLine));
                            }
                        }
                    }
                
                    if (itemsAdded >= MaxItems)
                    {
                        break;
                    }
                }
                
                ViewPort.SetItems(Items, _definition.Wrap);
                ViewPort.SetTotalRows(Items.Count);
            }

            NumberOfScoredItems = NumberOfItems;
            Searching = false;
            View.SetListBoxItems();
            _previewSignal.Set();
            Interlocked.Exchange(ref _lastSearchVersion, currentSearchVersion);
            return;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism
        };

        var ct = CancellationToken.None;

        // Apply pre-parse function if defined
        var pattern = FuzzySearcher.ParsePattern(CaseMode.CaseSmart, parsedSearchString, true);
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

                    if (_definition.PreFilter != null && !_definition.PreFilter.Invoke(line, currentSearchString))
                    {
                        continue;
                    }
                        
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
            return;
        }

        foreach (var localData in _localResultsPool)
        {
            globalList.AddRange(localData.Entries);
            localData.Entries.Clear();
        }
        
        if (_definition.PostProcess != null)
        {
            _definition.PostProcess(currentSearchString, globalList);
        }
        else
        {
            globalList.Sort(_definition.FinalComparer);
        }

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
                    
                    if (item.Item is TerminalEscapedLine escapedLine)
                    {
                        escapedLine.SetPos(pos);
                        Items.Add((item.Item, escapedLine));
                    }
                    else
                    {
                        if (fullFilePath != null)
                        {
                            var segment = new TextSegment { State = new AnsiState(), Text = fullFilePath };
                            var segments = new List<TextSegment> { segment };
                            var line = new EscapedLine(segments);
                            var terminalEscapedLine = new TerminalEscapedLine();
                            terminalEscapedLine.Lines.Add(line);
                            terminalEscapedLine.SetPos(pos);
                            Items.Add((item.Item, terminalEscapedLine));
                        }
                    }
                }
            }

            ViewPort.SetItems(Items, _definition.Wrap);
            ViewPort.SetTotalRows(Items.Count);
        }

        View.SetListBoxItems();
        _previewSignal.Set();

        Searching = false;
        Interlocked.Exchange(ref _lastSearchVersion, currentSearchVersion);
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
        _showPreview = definition.HasPreview;
        View.Show(definition.HasPreview);
        _chunks.Clear();
        _chunks.Add(new Chunk());
        lock (_searchStringLock)
        {
            _searchString = definition.SearchString;
        }
        View.SetSearchString(definition.SearchString);
        _definition = definition;
        _currentDefinitionCancellationTokenSource = new CancellationTokenSource();
        if (definition.Header != null)
        {
            View.SetHeader(definition.Header);
        }
        else
        {
            View.HideHeader();
        }
        if (definition.AsyncFunction != null)
        {
            var channel = Channel.CreateUnbounded<object>(_channelOptions);
            var writerTask = definition.AsyncFunction(channel.Writer, _currentDefinitionCancellationTokenSource.Token).ConfigureAwait(false);
            await ReadFromSourceAsync(channel.Reader, _currentDefinitionCancellationTokenSource.Token).ConfigureAwait(false);
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
                        Interlocked.Increment(ref _searchVersion);
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
        Interlocked.Increment(ref _searchVersion);
        _restartSearchSignal.Set();
    }
    
    public void SetSearchString(string message)
    {
        lock (_searchStringLock)
        {
            _searchString = message;
        }

        Interlocked.Increment(ref _searchVersion);
        _restartSearchSignal.Set();
    }

    private string GetCharFromKey(int key, ModifierKeys eKeyModifiers)
    {
        bool shiftPressed = (eKeyModifiers & ModifierKeys.LShift) != 0 || (eKeyModifiers & ModifierKeys.RShift) != 0;

        if (key >= 0x30 && key <= 0x39)
        {
            return ((char)key).ToString();
        }

        if (key >= 0x41 && key <= 0x5A)
        {
            char ch = (char)key;
            if (!shiftPressed)
            {
                ch = char.ToLower(ch);
            }
            return ch.ToString();
        }

        return string.Empty;
    }

    public void HandlePreviewKey(int key, ModifierKeys eKeyModifiers)
    {
        if ((DateTime.Now - _previewVimLastKeyPressTime) > _previewVimTimeout)
        {
            _previewVimState = string.Empty;
        }
        _previewVimLastKeyPressTime = DateTime.Now;
        
        var resetState = false;
        switch (eKeyModifiers)
        {
            case ModifierKeys.None:
                switch (key)
                {
                    case VirtualKeyCodes.VK_ESCAPE:
                        if (PreviewViewPort.Mode == PreviewViewport.PreviewMode.Visual)
                        {
                            PreviewViewPort.ToggleVisualMode();
                        }
                        else
                        {
                            View.FocusSearch();
                            PreviewViewPort.Focused = false;
                        }
                        View.TriggerPreviewRender();
                        resetState = true;
                        break;
                    case VirtualKeyCodes.VK_DOWN:
                        PreviewViewPort.SelectNextLine();
                        View.SetPreviewLines(PreviewViewPort.ViewportLines());
                        resetState = true;
                        break;
                    case VirtualKeyCodes.VK_UP:
                        PreviewViewPort.SelectPreviousLine();
                        View.SetPreviewLines(PreviewViewPort.ViewportLines());
                        resetState = true;
                        break;
                    case VirtualKeyCodes.VK_NEXT:
                        PreviewViewPort.SelectHalfPageDown();
                        View.SetPreviewLines(PreviewViewPort.ViewportLines());
                        resetState = true;
                        break;
                    case VirtualKeyCodes.VK_PRIOR:
                        PreviewViewPort.SelectHalfPageUp();
                        View.SetPreviewLines(PreviewViewPort.ViewportLines());
                        resetState = true;
                        break;
                }
                break;
        }

        if (resetState)
        {
            _previewVimState = string.Empty;
            return;
        }
        
        var keyChar = GetCharFromKey(key, eKeyModifiers);
        _previewVimState += keyChar;

        switch (_previewVimState)
        {
            case "gg":
                PreviewViewPort.SelectToTop();
                View.SetPreviewLines(PreviewViewPort.ViewportLines());
                _previewVimState = string.Empty;
                break;
            case "G":
                PreviewViewPort.SelectToBottom();
                View.SetPreviewLines(PreviewViewPort.ViewportLines());
                _previewVimState = string.Empty;
                break;
            case "j":
                PreviewViewPort.SelectNextLine();
                View.TriggerPreviewRender();
                _previewVimState = string.Empty;
                break;
            case "k":
                PreviewViewPort.SelectPreviousLine();
                View.TriggerPreviewRender();
                _previewVimState = string.Empty;
                break;
            case "v":
                PreviewViewPort.ToggleVisualMode();
                View.TriggerPreviewRender();
                _previewVimState = string.Empty;
                break;
            case "y":
                PreviewViewPort.CopySelected(View);
                break;
        }
    }
    
    public async Task HandleKeyUp(object item, int eKey, ModifierKeys eKeyModifiers)
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
            else if (eKey == VirtualKeyCodes.VK_V)
            {
                PreviewViewPort.ToggleVisualMode();
            }
            else if (eKey == VirtualKeyCodes.VK_W)
            {
                if (_showPreview)
                {
                    PreviewViewPort.Focused = !PreviewViewPort.Focused;
                    View.FocusPreview();
                    View.SetPreviewLines(PreviewViewPort.ViewportLines());
                }
            }
            else if (_definition != null && _definition.KeyBindings.TryGetValue((eKeyModifiers, eKey), out var action))
            {
                await action(item);
            }
            else if (GlobalKeyBindings.TryGetValue((eKeyModifiers, eKey), out var globalAction))
            {
                await globalAction(item, this);
            }
        }
    }

    public Task ShowToast(string message, int duration = 3000)
    {
        View.ShowToast(message, duration);
        return Task.CompletedTask;
    }

    public Task Clear()
    {
        return Task.CompletedTask;
    }

    public Task Close(bool quit)
    {
        _currentDefinitionCancellationTokenSource?.Cancel();
        View.Hide(quit);
        return Task.CompletedTask;
    }

    public void TogglePreview()
    {
        _showPreview = !_showPreview;
        _previewSignal.Set();
        View.TogglePreview(_showPreview); 
    }

    public void RenderImage(MemoryStream memoryStream)
    {
        var bitmap = new Bitmap(memoryStream);
        View.ShowImagePreview(bitmap);
    }

    public void RenderText(List<List<TextSegment>> lines, int startLine)
    {
        PreviewViewPort.SetLines(lines, startLine);
        View.SetPreviewLines(PreviewViewPort.ViewportLines());
    }

    public void RenderError(string errorInfo)
    {
        View.SetPreviewLines(TextSegment.BasicText(errorInfo));
    }

    public void SetPreviewHeight(int height)
    {
        _previewHeight = height;
    }

    public void SetNumberOfRows(int rows)
    {
        lock (_snapshotLock)
        {
            _viewport = new Viewport(rows);
        }
    }

    public void SetPreviewNumberOfRow(int rows)
    {
        _previewViewport = new PreviewViewport(rows);
    }

    public void SelectNext()
    {
        lock (_snapshotLock)
        {
            ViewPort.SelectNext();
        }
        View.SetListBoxItems();
        _previewSignal.Set();
    }
    
    public void SelectPageDown()
    {
        lock (_snapshotLock)
        {
            ViewPort.PageDown();
        }
        View.SetListBoxItems();
        _previewSignal.Set();
    }
    
    public void SelectPageUp()
    {
        lock (_snapshotLock)
        {
            ViewPort.PageUp();
        }
        View.SetListBoxItems();
        _previewSignal.Set();
    }

    public void SelectPrevious()
    {
        lock (_snapshotLock)
        {
            ViewPort.SelectPrevious();
        }
        View.SetListBoxItems();
        _previewSignal.Set();
    }

    public async Task OnReturn(object item)
    {
        if (_definition is null)
        {
            return;
        }
        
        await _definition.ResultHandler.HandleAsync(item);
    }
    
    public void OnEscape()
    {
        if (PreviewViewPort.Focused)
        {
            PreviewViewPort.Focused = false;
            View.SetPreviewLines(PreviewViewPort.ViewportLines());
        }
        else
        {
            if (_definition != null && _definition.OnClosed != null)
            {
                _definition.OnClosed();
                Close(false);
            }
            else if (_definition != null && _definition.QuitOnEscape)
            {
                Environment.Exit(0);
            }
        }
    }

    public void PreviewHalfPageUp()
    {
        PreviewViewPort.HalfPageUp();
        View.SetPreviewLines(PreviewViewPort.ViewportLines());
    }

    public void PreviewHalfPageDown()
    {
        PreviewViewPort.HalfPageDown();
        View.SetPreviewLines(PreviewViewPort.ViewportLines());
    }

    public void ShowLastDefinition()
    {
        if (_definition is null)
        {
            return;
        }
        
        View.Show(_definition.HasPreview);
    }

    public MenuDefinition? GetMenuDefinition()
    {
        return _definition;
    }

    public List<object> GetAllCurrentSearchResults()
    {
        lock (_snapshotLock)
        {
            return Items.Select(item => item.Item1).ToList();
        }
    }
}