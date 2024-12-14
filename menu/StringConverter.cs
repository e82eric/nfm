using System;

namespace nfm.menu;

public class StringConverter : ITtoStrConverter<string>
{
    public ReadOnlySpan<char> Convert(string t, Span<char> buf)
    {
        return t;
    }
}