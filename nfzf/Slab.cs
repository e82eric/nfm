namespace nfzf;

public class Slab
{
    private readonly int[] _intSlab;
    private readonly char[] _charSlab;
    private int _intOffset;
    private int _charOffset;

    public static Slab MakeDefault()
    {
        return new Slab(100 * 1024, 100 * 1024);
    }

    public int Cap => _intSlab.Length;

    public Slab(int intCapacity, int charCapacity)
    {
        _intSlab = new int[intCapacity];
        _charSlab = new char[charCapacity];
        _intOffset = 0;
        _charOffset = 0;
    }

    public Span<int> AllocInt(int size)
    {
        if (_intOffset + size > _intSlab.Length)
        {
            return new int[size];
        }

        var slice = new Span<int>(_intSlab, _intOffset, size);
        _intOffset += size;
        return slice;
    }

    public Span<char> AllocChar(int size)
    {
        if (_charOffset + size > _charSlab.Length)
        {
            return new char[size];
        }

        var slice = new Span<char>(_charSlab, _charOffset, size);
        _charOffset += size;
        return slice;
    }

    public void Reset()
    {
        _intOffset = 0;
        _charOffset = 0;
    }
}
