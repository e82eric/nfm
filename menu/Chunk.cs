namespace nfm.menu;

class Chunk<T>
{
    public int Size { get; private set; } = 0;
    public readonly T[] Items;
    public const int MaxSize = 10000;
    private bool _manualComplete = false;

    public Chunk()
    {
        Items = new T[MaxSize];
    }

    public void SetComplete()
    {
        _manualComplete = true;
    }

    public bool IsComplete => _manualComplete || Size >= MaxSize;

    public bool TryAdd(T val)
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
