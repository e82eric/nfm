using System.Drawing;
using nfm.menu;

namespace Core;

public class TextSegment
{
    public required string Text { get; init; }
    public required AnsiState State { get; init; }

    public static List<List<TextSegment>> BasicText(string val)
    {
        return [ new() { new() { State = new AnsiState(), Text = val } } ];
    }

    public static List<TextSegment> BlankLine()
    {
        return [new() { State = new AnsiState(), Text = string.Empty }];
    }
}

public class AnsiState
{
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikethrough { get; set; }

    // We'll default to "Console-style" defaults:
    // Foreground = Gray-ish, Background = Black
    public Color Foreground { get; set; } = ColorTranslator.FromHtml("#a89984");
    public Color Background { get; set; } = ColorTranslator.FromHtml("#282828");

    // Makes a copy
    public AnsiState Clone()
    {
        return new AnsiState
        {
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            Strikethrough = Strikethrough,
            Foreground = Foreground,
            Background = Background
        };
    }
}

public static class TerminalEscapeCodeConverter
{
    // We keep track of every color we encounter in a dictionary so we can build
    // the color table in the final RTF. Key: Color, Value: index in RTF colortbl.

    // The main entry point
    public static List<List<TextSegment>> Convert(List<string> text)
    {
        // Reset color map each time we convert
        //new Dictionary<Color, int>();

        var segments = new List<List<TextSegment>>();
        foreach (var line in text)
        {
            segments.Add(Convert(line));
        }

        return segments;
    }

    public static EscapedLine Parse(string line)
    {
        var lines = Convert(line);
        return new EscapedLine(lines);
    }

    // public static List<Line> ConvertToColoredListBoxLine(string lineText, IList<int> pos)
    // {
    //     var result = new List<Line>();
    //     var splitLines = lineText.Split(["\r\n", "\n"], StringSplitOptions.None);
    //
    //     var accumulatedLength = 0;
    //     foreach (var split in splitLines)
    //     {
    //         var segments = Convert(split);
    //         var textLength = segments.Select(s => s.Text.Length).Sum();
    //         var linePos = new List<int>();
    //         foreach (var p in pos)
    //         {
    //             if (p >= accumulatedLength && p < accumulatedLength + textLength)
    //             {
    //                 var adjustedP = p - accumulatedLength;
    //                 linePos.Add(adjustedP);
    //             }
    //         }
    //
    //         var line = new Line(split, segments, linePos);
    //         result.Add(line);
    //         accumulatedLength += textLength;
    //     }
    //
    //     return result;
    // }

    /// <summary>
    /// Splits the input text into segments of plain text, each associated with
    /// the final AnsiState after applying any preceding escape codes.
    /// </summary>
    public static List<TextSegment> Convert(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return TextSegment.BlankLine();
        }

        var segments = new List<TextSegment>();
        var currentState = new AnsiState();
    
        int i = 0;
        while (i < text.Length)
        {
            // Find the next ESC character (ASCII 27)
            int escPos = text.IndexOf('\x1B', i);
            if (escPos == -1)
            {
                // No more escape sequences, remainder of the text is plain
                if (i < text.Length)
                {
                    string rawText = text.Substring(i);
                    if (!string.IsNullOrEmpty(rawText))
                    {
                        segments.Add(new TextSegment
                        {
                            Text = rawText,
                            State = currentState.Clone()
                        });
                    }
                }
                break; 
            }

            // If there's text before the ESC, add it as plain text
            if (escPos > i)
            {
                string rawText = text.Substring(i, escPos - i);
                if (!string.IsNullOrEmpty(rawText))
                {
                    segments.Add(new TextSegment
                    {
                        Text = rawText,
                        State = currentState.Clone()
                    });
                }
            }

            // Now we've found ESC at escPos. Check if the next char is '[' (start of CSI)
            if (escPos + 1 < text.Length && text[escPos + 1] == '[')
            {
                // Find the trailing 'm' of the SGR sequence
                int mPos = text.IndexOf('m', escPos + 2);
                if (mPos == -1)
                {
                    // If no 'm' found, treat everything from escPos on as plain text or just break
                    // We’ll just add it as plain text and break.
                    string rawText = text.Substring(escPos);
                    segments.Add(new TextSegment
                    {
                        Text = rawText,
                        State = currentState.Clone()
                    });
                    break;
                }
                else
                {
                    // Extract the entire escape sequence, e.g. "\x1B[38;5;203m"
                    string ansiEscape = text.Substring(escPos, (mPos - escPos + 1));

                    // Apply the codes to currentState
                    ApplySgrCodes(currentState, ansiEscape);

                    // Advance past this escape sequence
                    i = mPos + 1;
                }
            }
            else
            {
                // We found an ESC that does not look like SGR (e.g. ESC not followed by '[')
                // For simplicity, treat it as plain text and move on.
                segments.Add(new TextSegment
                {
                    Text = text.Substring(escPos, 1),
                    State = currentState.Clone()
                });
                i = escPos + 1;
            }
        }

        return segments;
    }

    /// <summary>
    /// Applies one or more SGR (Select Graphic Rendition) codes to the current state.
    /// Example: ESC[1;31m => bold on, fg color = red
    /// Example: ESC[38;5;203m => 256-color foreground
    /// </summary>
    private static void ApplySgrCodes(AnsiState state, string ansiEscape)
    {
        // ansiEscape is something like "\x1B[38;5;203m"
        // We'll strip off the prefix "\x1B[" and suffix "m"
        string inner = ansiEscape.Substring(2, ansiEscape.Length - 3); // e.g. "38;5;203"

        // If there's nothing, e.g. ESC[m => means reset
        if (inner.Length == 0)
        {
            ResetState(state);
            return;
        }

        // e.g. "1;34" => ["1","34"], or "38;5;203" => ["38","5","203"]
        string[] parts = inner.Split(';');
        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int code))
            {
                // If parse fails, ignore
                continue;
            }

            switch (code)
            {
                case 0:
                    // Reset all
                    ResetState(state);
                    break;

                case 1: // Bold on
                    state.Bold = true;
                    break;
                case 22: // Bold off (or normal intensity)
                    state.Bold = false;
                    break;

                case 3: // Italic on
                    state.Italic = true;
                    break;
                case 23: // Italic off
                    state.Italic = false;
                    break;

                case 4: // Underline on
                    state.Underline = true;
                    break;
                case 24: // Underline off
                    state.Underline = false;
                    break;

                case 9: // Strikethrough on
                    state.Strikethrough = true;
                    break;
                case 29: // Strikethrough off
                    state.Strikethrough = false;
                    break;

                // 30-37 => standard 8 foreground colors
                case >= 30 and <= 37:
                {
                    int idx = code - 30; // 0-7
                    state.Foreground = AnsiColorMap8[idx];
                    break;
                }
                // 90-97 => bright 8 foreground colors
                case >= 90 and <= 97:
                {
                    int idx = code - 90; // 0-7
                    state.Foreground = AnsiColorMap8Bright[idx];
                    break;
                }
                // 40-47 => standard 8 background
                case >= 40 and <= 47:
                {
                    int idx = code - 40;
                    //state.Background = AnsiColorMap8[idx];
                    break;
                }
                // 100-107 => bright 8 background
                case >= 100 and <= 107:
                {
                    int idx = code - 100;
                    //state.Background = AnsiColorMap8Bright[idx];
                    break;
                }

                // 38 => might be 38;5;N (256 color fg) or 38;2;R;G;B (24-bit)
                // 48 => same for background
                // We'll do a partial parse for 38;5;N or 48;5;N
                case 38:
                case 48:
                    // We'll look ahead in parts
                    // E.g. "38;5;203" => parts[0] = "38", parts[1]="5", parts[2]="203"
                    // We'll parse index 1 -> we expect "5", index 2 -> the color number
                    // If "5", then it's a 256-color index
                    // If "2", it's 24-bit color (R;G;B).
                    HandleExtendedColor(state, parts, code == 38);
                    break;

                default:
                    // We ignore other codes for brevity (like faint=2, blink=5, etc.)
                    break;
            }
        }
    }

    private static void ResetState(AnsiState state)
    {
        state.Bold = false;
        state.Italic = false;
        state.Underline = false;
        state.Strikethrough = false;
        state.Foreground = ColorTranslator.FromHtml("#a89984");
        state.Background = ColorTranslator.FromHtml("#282828");
    }

    private static void HandleExtendedColor(AnsiState state, string[] parts, bool isForeground)
    {
        // parts might look like ["38","5","203"] => means FG = xterm256 color #203
        // or ["38","2","R","G","B"] => means FG = truecolor (24-bit)
        // For brevity, we handle only the 5 case (256 color).
        // If user wants 24-bit, parse the "2;R;G;B" scenario similarly.

        // find "5" or "2" in the array
        // The first item is "38" or "48", so we start from index of that item, then next item might be "5" or "2".
        // We'll find the index of code "38"/"48" inside parts:
        int index = Array.IndexOf(parts, isForeground ? "38" : "48");
        if (index < 0 || index >= parts.Length - 1) return;

        if (parts[index + 1] == "5")
        {
            // Then the next item is the color index
            if (index + 2 < parts.Length && int.TryParse(parts[index + 2], out int colorIndex))
            {
                // 256-color look-up
                Color c = XTerm256Color(colorIndex);
                if (isForeground)
                    state.Foreground = c;
                else
                    state.Background = c;
            }
        }
        else if (parts[index + 1] == "2" && index + 3 < parts.Length)
        {
            // 24-bit color => 38;2;R;G;B or 48;2;R;G;B
            // We'll do minimal error checking:
            if (int.TryParse(parts[index + 2], out int r) &&
                int.TryParse(parts[index + 3], out int g) &&
                int.TryParse(parts[index + 4], out int b))
            {
                // Clamp values 0..255
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);
                if (isForeground)
                    state.Foreground = Color.FromArgb(r, g, b);
                //else
                //state.Background = Color.FromArgb(r, g, b);
            }
        }
    }

    // Standard 8 ANSI colors (30-37 foreground, 40-47 background)
    private static readonly Color[] AnsiColorMap8 =
    {
        Color.Black,       // 0 => black
        Color.Red,         // 1 => red
        Color.Green,       // 2 => green
        Color.Yellow,      // 3 => yellow
        Color.Blue,        // 4 => blue
        Color.Magenta,     // 5 => magenta
        Color.Cyan,        // 6 => cyan
        Color.LightGray    // 7 => light gray
    };

    // Bright versions (90-97 / 100-107)
    private static readonly Color[] AnsiColorMap8Bright =
    {
        Color.DarkGray,   // bright black
        Color.LightCoral, // bright red (pick something close)
        Color.LightGreen,
        Color.LightYellow,
        Color.LightBlue,
        Color.LightPink,  // bright magenta
        Color.LightCyan,
        Color.White
    };

    /// <summary>
    /// For 256-color codes ESC[38;5;N or ESC[48;5;N], map N to an RGB color in the xterm palette.
    /// Basic logic: 
    ///  0-15 = standard + bright 
    /// 16-231 = 6x6x6 color cube 
    /// 232-255 = grayscale ramp
    /// </summary>
    private static Color XTerm256Color(int index)
    {
        if (index < 0) index = 0;
        if (index > 255) index = 255;

        // 0-7:  standard
        // 8-15: bright
        // 16-231: 6x6x6 color cube
        // 232-255: grayscale ramp
        if (index < 16)
        {
            // Reuse our existing arrays (0-7, 8-15)
            if (index < 8) return AnsiColorMap8[index];
            else return AnsiColorMap8Bright[index - 8];
        }
        else if (index < 232)
        {
            // 16 -> color 0,0,0
            // each component in [0..5]
            int cIndex = index - 16; // 0..215
            int r = cIndex / 36;     // 0..5
            int g = (cIndex % 36) / 6; 
            int b = cIndex % 6;

            // Each component is in [0..5], map to 0..255 (but xterm uses steps of 51)
            int R = r * 51;
            int G = g * 51;
            int B = b * 51;
            return Color.FromArgb(R, G, B);
        }
        else
        {
            // 232..255 => grayscale
            // 232 => rgb(8,8,8)
            // 255 => rgb(238,238,238)
            int level = (index - 232) * 10 + 8;
            return Color.FromArgb(level, level, level);
        }
    }
}
