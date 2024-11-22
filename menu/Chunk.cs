namespace nfm.menu;

struct ItemScoreResult(int score, int index)
{
    public int Score { get; set; } = score;
    public int Index { get; set; } = index;
}

class Chunk
{
    private int _size;
    public readonly string[] Items;
    private const int MaxSize = 1000;
    private bool _manualComplete;
    private string? _queryString;
    private readonly ItemScoreResult[] _resultCache;
    private int _resultCacheSize;

    public Chunk()
    {
        _size = 0;
        _manualComplete = false;
        Items = new string[MaxSize];
        _resultCache = new ItemScoreResult[MaxSize];
        _queryString = null;
    }
    
    public void SetQueryStringNoReset(string queryString)
    {
        _queryString = queryString;
        //if (string.IsNullOrEmpty(queryString))
        //{
        //    _resultCacheSize = 0;
        //}
    }

    public bool TryGetResultCache(string queryString, ref ItemScoreResult[]? resultCache, out int size)
    {
        if (!string.IsNullOrEmpty(_queryString) && !string.IsNullOrEmpty(queryString))
        {
            if (queryString == _queryString)
            {
                resultCache = _resultCache;
                size = _resultCacheSize;
                return true;
            }
        }

        size = -1;
        return false;
    }

    public bool TryGetItemCache(string queryString, ref ItemScoreResult[]? cache, out int size)
    {
        if (_resultCacheSize > 0 && !string.IsNullOrEmpty(_queryString) && queryString.StartsWith(_queryString))
        {
            cache = _resultCache;
            size = _resultCacheSize;
            return true;
        }

        size = -1;
        return false;
    }
    
    public void SetResultCacheItemNoReset(int index, int score, int i)
    {
        _resultCache[i].Index = index;
        _resultCache[i].Score = score;
        _resultCacheSize = i + 1;
    }

    public void SetComplete()
    {
        _manualComplete = true;
    }

    public bool IsComplete => _manualComplete || _size >= MaxSize;

    public bool TryAdd(string val)
    {
        if (IsComplete)
        {
            return false;
        }
        
        Items[_size] = val;
        _size++;
        return true;
    }
}