using System;

namespace nfm.menu;

public interface ITtoStrConverter<in T>
{
    public ReadOnlySpan<char> Convert(T t, Span<char> buf);
}