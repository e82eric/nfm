namespace nfm.Ui.Core;

[Flags]
public enum ModifierKeys
{
    None = 0,
    LShift = 1 << 0,
    RShift = 1 << 1,
    LAlt = 1 << 2,
    RAlt = 1 << 3,
    LCtl = 1 << 4,
    LWin = 1 << 5,
    RWin = 1 << 6
}