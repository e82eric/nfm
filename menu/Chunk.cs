namespace nfm.menu;

class Chunk
{
    public int Size { get; private set; } = 0;
    public readonly string[] Items = new string[MaxSize];
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
        
        Items[Size] = val;
        Size++;
        return true;
    }
}