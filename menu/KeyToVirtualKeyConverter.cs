using System.Collections.Generic;
using Avalonia.Input;

namespace nfm.menu;

public static class KeyToVirtualKeyConverter
{
    private static readonly Dictionary<Key, int> KeyToVirtualKeyMap = new()
    {
        { Key.Back, VirtualKeyCodes.VK_BACK },
        { Key.Tab, VirtualKeyCodes.VK_TAB },
        { Key.Clear, VirtualKeyCodes.VK_CLEAR },
        { Key.Return, VirtualKeyCodes.VK_RETURN },
        { Key.Pause, VirtualKeyCodes.VK_PAUSE },
        { Key.CapsLock, VirtualKeyCodes.VK_CAPITAL },
        { Key.Escape, VirtualKeyCodes.VK_ESCAPE },
        { Key.Space, VirtualKeyCodes.VK_SPACE },
        { Key.PageUp, VirtualKeyCodes.VK_PRIOR },
        { Key.PageDown, VirtualKeyCodes.VK_NEXT },
        { Key.End, VirtualKeyCodes.VK_END },
        { Key.Home, VirtualKeyCodes.VK_HOME },
        { Key.Left, VirtualKeyCodes.VK_LEFT },
        { Key.Up, VirtualKeyCodes.VK_UP },
        { Key.Right, VirtualKeyCodes.VK_RIGHT },
        { Key.Down, VirtualKeyCodes.VK_DOWN },
        { Key.Select, VirtualKeyCodes.VK_SELECT },
        { Key.Print, VirtualKeyCodes.VK_PRINT },
        { Key.Execute, VirtualKeyCodes.VK_EXECUTE },
        { Key.Snapshot, VirtualKeyCodes.VK_SNAPSHOT },
        { Key.Insert, VirtualKeyCodes.VK_INSERT },
        { Key.Delete, VirtualKeyCodes.VK_DELETE },
        { Key.Help, VirtualKeyCodes.VK_HELP },

        // Number keys
        { Key.D0, VirtualKeyCodes.VK_0 },
        { Key.D1, VirtualKeyCodes.VK_1 },
        { Key.D2, VirtualKeyCodes.VK_2 },
        { Key.D3, VirtualKeyCodes.VK_3 },
        { Key.D4, VirtualKeyCodes.VK_4 },
        { Key.D5, VirtualKeyCodes.VK_5 },
        { Key.D6, VirtualKeyCodes.VK_6 },
        { Key.D7, VirtualKeyCodes.VK_7 },
        { Key.D8, VirtualKeyCodes.VK_8 },
        { Key.D9, VirtualKeyCodes.VK_9 },

        // Letter keys
        { Key.A, VirtualKeyCodes.VK_A },
        { Key.B, VirtualKeyCodes.VK_B },
        { Key.C, VirtualKeyCodes.VK_C },
        { Key.D, VirtualKeyCodes.VK_D },
        { Key.E, VirtualKeyCodes.VK_E },
        { Key.F, VirtualKeyCodes.VK_F },
        { Key.G, VirtualKeyCodes.VK_G },
        { Key.H, VirtualKeyCodes.VK_H },
        { Key.I, VirtualKeyCodes.VK_I },
        { Key.J, VirtualKeyCodes.VK_J },
        { Key.K, VirtualKeyCodes.VK_K },
        { Key.L, VirtualKeyCodes.VK_L },
        { Key.M, VirtualKeyCodes.VK_M },
        { Key.N, VirtualKeyCodes.VK_N },
        { Key.O, VirtualKeyCodes.VK_O },
        { Key.P, VirtualKeyCodes.VK_P },
        { Key.Q, VirtualKeyCodes.VK_Q },
        { Key.R, VirtualKeyCodes.VK_R },
        { Key.S, VirtualKeyCodes.VK_S },
        { Key.T, VirtualKeyCodes.VK_T },
        { Key.U, VirtualKeyCodes.VK_U },
        { Key.V, VirtualKeyCodes.VK_V },
        { Key.W, VirtualKeyCodes.VK_W },
        { Key.X, VirtualKeyCodes.VK_X },
        { Key.Y, VirtualKeyCodes.VK_Y },
        { Key.Z, VirtualKeyCodes.VK_Z },

        // Function keys
        { Key.F1, VirtualKeyCodes.VK_F1 },
        { Key.F2, VirtualKeyCodes.VK_F2 },
        { Key.F3, VirtualKeyCodes.VK_F3 },
        { Key.F4, VirtualKeyCodes.VK_F4 },
        { Key.F5, VirtualKeyCodes.VK_F5 },
        { Key.F6, VirtualKeyCodes.VK_F6 },
        { Key.F7, VirtualKeyCodes.VK_F7 },
        { Key.F8, VirtualKeyCodes.VK_F8 },
        { Key.F9, VirtualKeyCodes.VK_F9 },
        { Key.F10, VirtualKeyCodes.VK_F10 },
        { Key.F11, VirtualKeyCodes.VK_F11 },
        { Key.F12, VirtualKeyCodes.VK_F12 },
        { Key.F13, VirtualKeyCodes.VK_F13 },
        { Key.F14, VirtualKeyCodes.VK_F14 },
        { Key.F15, VirtualKeyCodes.VK_F15 },
        { Key.F16, VirtualKeyCodes.VK_F16 },
        { Key.F17, VirtualKeyCodes.VK_F17 },
        { Key.F18, VirtualKeyCodes.VK_F18 },
        { Key.F19, VirtualKeyCodes.VK_F19 },
        { Key.F20, VirtualKeyCodes.VK_F20 },
        { Key.F21, VirtualKeyCodes.VK_F21 },
        { Key.F22, VirtualKeyCodes.VK_F22 },
        { Key.F23, VirtualKeyCodes.VK_F23 },
        { Key.F24, VirtualKeyCodes.VK_F24 },

        // Modifier keys
        { Key.LeftShift, VirtualKeyCodes.VK_LSHIFT },
        { Key.RightShift, VirtualKeyCodes.VK_RSHIFT },
        { Key.LeftCtrl, VirtualKeyCodes.VK_LCONTROL },
        { Key.RightCtrl, VirtualKeyCodes.VK_RCONTROL },
        { Key.LeftAlt, VirtualKeyCodes.VK_LMENU },
        { Key.RightAlt, VirtualKeyCodes.VK_RMENU },

        // Numpad keys
        { Key.NumPad0, VirtualKeyCodes.VK_NUMPAD0 },
        { Key.NumPad1, VirtualKeyCodes.VK_NUMPAD1 },
        { Key.NumPad2, VirtualKeyCodes.VK_NUMPAD2 },
        { Key.NumPad3, VirtualKeyCodes.VK_NUMPAD3 },
        { Key.NumPad4, VirtualKeyCodes.VK_NUMPAD4 },
        { Key.NumPad5, VirtualKeyCodes.VK_NUMPAD5 },
        { Key.NumPad6, VirtualKeyCodes.VK_NUMPAD6 },
        { Key.NumPad7, VirtualKeyCodes.VK_NUMPAD7 },
        { Key.NumPad8, VirtualKeyCodes.VK_NUMPAD8 },
        { Key.NumPad9, VirtualKeyCodes.VK_NUMPAD9 },
        { Key.Multiply, VirtualKeyCodes.VK_MULTIPLY },
        { Key.Add, VirtualKeyCodes.VK_ADD },
        { Key.Separator, VirtualKeyCodes.VK_SEPARATOR },
        { Key.Subtract, VirtualKeyCodes.VK_SUBTRACT },
        { Key.Decimal, VirtualKeyCodes.VK_DECIMAL },
        { Key.Divide, VirtualKeyCodes.VK_DIVIDE },

        // Media keys
        { Key.MediaNextTrack, VirtualKeyCodes.VK_MEDIA_NEXT_TRACK },
        { Key.MediaPreviousTrack, VirtualKeyCodes.VK_MEDIA_PREV_TRACK },
        { Key.MediaStop, VirtualKeyCodes.VK_MEDIA_STOP },
        { Key.MediaPlayPause, VirtualKeyCodes.VK_MEDIA_PLAY_PAUSE },
        { Key.LaunchMail, VirtualKeyCodes.VK_LAUNCH_MAIL },
        { Key.SelectMedia, VirtualKeyCodes.VK_LAUNCH_MEDIA_SELECT },
        { Key.LaunchApplication1, VirtualKeyCodes.VK_LAUNCH_APP1 },
        { Key.LaunchApplication2, VirtualKeyCodes.VK_LAUNCH_APP2 }
    };

    public static int ConvertKeyToVirtualKey(Key key)
    {
        return KeyToVirtualKeyMap.TryGetValue(key, out int vkCode) ? vkCode : 0;
    }
}