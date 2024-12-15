namespace nfm.menu;

class Chunk
{
    public int Size { get; private set; } = 0;
    public readonly object[] Items;
    public const int MaxSize = 10000;
    private bool _manualComplete = false;

    public Chunk()
    {
        Items = new object[MaxSize];
    }

    public void SetComplete()
    {
        _manualComplete = true;
    }

    public bool IsComplete => _manualComplete || Size >= MaxSize;

    public bool TryAdd(object val)
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
