namespace nfm.menu;

class Chunk
{
    public int Size { get; private set; } = 0;
    public readonly (string, string)[] Items = new (string, string)[MaxSize];
    public const int MaxSize = 10000;
    private bool _manualComplete = false;

    public void SetComplete()
    {
        _manualComplete = true;
    }

    public bool IsComplete => _manualComplete || Size >= MaxSize;

    public bool TryAdd(string val)
    {
        if (IsComplete)
        {
            return false;
        }
        
        Items[Size].Item1 = val;
        Size++;
        return true;
    }
    public bool TryAdd((string, string) val)
    {
        if (IsComplete)
        {
            return false;
        }
        
        Items[Size] = val;
        Size++;
        return true;
    }
}
