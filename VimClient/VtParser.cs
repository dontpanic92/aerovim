// <copyright file="VtParser.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using System.Text;

/// <summary>
/// State-machine based ANSI/VT escape sequence parser that processes byte
/// streams from a PTY and updates a <see cref="TerminalBuffer"/>.
/// </summary>
public class VtParser
{
    // Standard ANSI colors in RGB format (red high, blue low).
    private static readonly int[] StandardColors =
    {
        0x000000, // 0 black
        0xCC0000, // 1 red
        0x00CC00, // 2 green
        0xCCCC00, // 3 yellow
        0x0000CC, // 4 blue
        0xCC00CC, // 5 magenta
        0x00CCCC, // 6 cyan
        0xCCCCCC, // 7 white
    };

    // Bright ANSI colors in RGB format.
    private static readonly int[] BrightColors =
    {
        0x555555, // 0 bright black
        0xFF5555, // 1 bright red
        0x55FF55, // 2 bright green
        0xFFFF55, // 3 bright yellow
        0x5555FF, // 4 bright blue
        0xFF55FF, // 5 bright magenta
        0x55FFFF, // 6 bright cyan
        0xFFFFFF, // 7 bright white
    };

    // 6x6x6 color cube component values.
    private static readonly int[] CubeValues = { 0, 95, 135, 175, 215, 255 };

    private readonly TerminalBuffer buffer;
    private readonly Action<string> titleChanged;
    private readonly Action<byte[]>? writeBack;
    private readonly List<int> parameters = new();
    private readonly List<int> subParameters = new();
    private readonly StringBuilder oscString = new();

    private VtState state = VtState.Ground;
    private bool privateMarker;

    private int currentParam;
    private bool hasCurrentParam;
    private int currentSubParam = -1;
    private bool inSubParam;

    // UTF-8 decoding state
    private int utf8BytesRemaining;
    private int utf8CodePoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="VtParser"/> class.
    /// </summary>
    /// <param name="buffer">The terminal buffer to update.</param>
    /// <param name="titleChanged">Callback invoked when the window title changes.</param>
    /// <param name="writeBack">Optional callback to write response bytes back to the PTY.</param>
    public VtParser(TerminalBuffer buffer, Action<string> titleChanged, Action<byte[]>? writeBack = null)
    {
        this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        this.titleChanged = titleChanged;
        this.writeBack = writeBack;
    }

    private enum VtState
    {
        Ground,
        Escape,
        CsiParam,
        CsiIntermediate,
        OscString,
        OscStringEsc,
    }

    /// <summary>
    /// Process a span of bytes from the PTY output.
    /// </summary>
    /// <param name="data">The raw byte data to process.</param>
    public void Process(ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            switch (this.state)
            {
                case VtState.Ground:
                    this.ProcessGround(b);
                    break;
                case VtState.Escape:
                    this.ProcessEscape(b);
                    break;
                case VtState.CsiParam:
                    this.ProcessCsiParam(b);
                    break;
                case VtState.CsiIntermediate:
                    this.ProcessCsiIntermediate(b);
                    break;
                case VtState.OscString:
                    this.ProcessOscString(b);
                    break;
                case VtState.OscStringEsc:
                    this.ProcessOscStringEsc(b);
                    break;
            }
        }
    }

    private static int PackRgb(int r, int g, int b)
    {
        return (r << 16) | (g << 8) | b;
    }

    private static int Convert256Color(int index)
    {
        if (index < 8)
        {
            return StandardColors[index];
        }

        if (index < 16)
        {
            return BrightColors[index - 8];
        }

        if (index < 232)
        {
            int n = index - 16;
            int ri = n / 36;
            int gi = (n / 6) % 6;
            int bi = n % 6;
            return PackRgb(CubeValues[ri], CubeValues[gi], CubeValues[bi]);
        }

        // Grayscale: 232-255
        int level = 8 + (10 * (index - 232));
        level = Math.Clamp(level, 0, 255);
        return PackRgb(level, level, level);
    }

    private static int ParseOscColorSpec(string spec)
    {
        if (spec.StartsWith("rgb:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = spec.Substring(4).Split('/');
            if (parts.Length == 3)
            {
                int r = ParseColorComponent(parts[0]);
                int g = ParseColorComponent(parts[1]);
                int b = ParseColorComponent(parts[2]);
                if (r >= 0 && g >= 0 && b >= 0)
                {
                    return PackRgb(r, g, b);
                }
            }
        }
        else if (spec.StartsWith("#", StringComparison.Ordinal))
        {
            string hex = spec.Substring(1);
            if (hex.Length == 6)
            {
                if (int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r)
                    && int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g)
                    && int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
                {
                    return PackRgb(r, g, b);
                }
            }
            else if (hex.Length == 3)
            {
                if (int.TryParse(hex.Substring(0, 1), System.Globalization.NumberStyles.HexNumber, null, out int r)
                    && int.TryParse(hex.Substring(1, 1), System.Globalization.NumberStyles.HexNumber, null, out int g)
                    && int.TryParse(hex.Substring(2, 1), System.Globalization.NumberStyles.HexNumber, null, out int b))
                {
                    return PackRgb(r * 17, g * 17, b * 17);
                }
            }
        }

        return -1;
    }

    private static int ParseColorComponent(string hex)
    {
        if (hex.Length < 1 || hex.Length > 4)
        {
            return -1;
        }

        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int value))
        {
            return hex.Length switch
            {
                1 => value * 17,          // 0-F → 0-255
                2 => value,               // 00-FF → 0-255
                3 => value >> 4,          // 000-FFF → 0-255
                4 => value >> 8,          // 0000-FFFF → 0-255
                _ => -1,
            };
        }

        return -1;
    }

    private void ProcessGround(byte b)
    {
        // Handle UTF-8 continuation bytes
        if (this.utf8BytesRemaining > 0)
        {
            if (b >= 0x80 && b <= 0xBF)
            {
                this.utf8CodePoint = (this.utf8CodePoint << 6) | (b & 0x3F);
                this.utf8BytesRemaining--;
                if (this.utf8BytesRemaining == 0)
                {
                    this.buffer.PutChar(this.utf8CodePoint);
                }

                return;
            }

            // Invalid continuation — reset and fall through
            this.utf8BytesRemaining = 0;
        }

        if (b >= 0x20 && b <= 0x7E)
        {
            this.buffer.PutChar(b);
        }
        else if (b >= 0xC0 && b <= 0xDF)
        {
            this.utf8CodePoint = b & 0x1F;
            this.utf8BytesRemaining = 1;
        }
        else if (b >= 0xE0 && b <= 0xEF)
        {
            this.utf8CodePoint = b & 0x0F;
            this.utf8BytesRemaining = 2;
        }
        else if (b >= 0xF0 && b <= 0xF7)
        {
            this.utf8CodePoint = b & 0x07;
            this.utf8BytesRemaining = 3;
        }
        else
        {
            this.ProcessControlChar(b);
        }
    }

    private void ProcessControlChar(byte b)
    {
        switch (b)
        {
            case 0x1B: // ESC
                this.state = VtState.Escape;
                break;
            case 0x08: // BS
                this.buffer.MoveCursorBack(1);
                break;
            case 0x09: // TAB — advance to next tab stop (every 8 columns)
                int nextTab = ((this.buffer.CursorCol / 8) + 1) * 8;
                this.buffer.SetCursorPosition(this.buffer.CursorRow, Math.Min(nextTab, this.buffer.Cols - 1));
                break;
            case 0x0A: // LF
            case 0x0B: // VT
            case 0x0C: // FF
                this.buffer.LineFeed();
                break;
            case 0x0D: // CR
                this.buffer.CarriageReturn();
                break;
            case 0x07: // BEL — ignore in ground state
                break;
        }
    }

    private void ProcessEscape(byte b)
    {
        switch (b)
        {
            case 0x5B: // '['
                this.EnterCsi();
                break;
            case 0x5D: // ']'
                this.oscString.Clear();
                this.state = VtState.OscString;
                break;
            case (byte)'7': // DECSC — save cursor
            case (byte)'s':
                this.buffer.SaveCursor();
                this.state = VtState.Ground;
                break;
            case (byte)'8': // DECRC — restore cursor
            case (byte)'u':
                this.buffer.RestoreCursor();
                this.state = VtState.Ground;
                break;
            case (byte)'M': // RI — reverse index
                this.buffer.ReverseIndex();
                this.state = VtState.Ground;
                break;
            case (byte)'D': // IND — index
                this.buffer.LineFeed();
                this.state = VtState.Ground;
                break;
            case (byte)'c': // RIS — full reset
                this.buffer.Reset();
                this.state = VtState.Ground;
                break;
            case 0x5C: // '\' — ST (string terminator, ignore if no active string)
                this.state = VtState.Ground;
                break;
            default:
                // Unrecognized escape sequence — return to ground
                this.state = VtState.Ground;
                break;
        }
    }

    private void ProcessCsiParam(byte b)
    {
        if (b >= (byte)'0' && b <= (byte)'9')
        {
            if (this.inSubParam)
            {
                this.currentSubParam = (this.currentSubParam * 10) + (b - (byte)'0');
            }
            else
            {
                this.currentParam = (this.currentParam * 10) + (b - (byte)'0');
                this.hasCurrentParam = true;
            }
        }
        else if (b == (byte)';')
        {
            this.FinalizeParam();
        }
        else if (b == (byte)':')
        {
            this.inSubParam = true;
            this.currentSubParam = 0;
        }
        else if (b == (byte)'?')
        {
            this.privateMarker = true;
        }
        else if (b >= 0x20 && b <= 0x2F)
        {
            this.FinalizeParam();
            this.state = VtState.CsiIntermediate;
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            this.FinalizeParam();
            this.DispatchCsi((char)b);
            this.state = VtState.Ground;
        }
        else if (b == 0x1B)
        {
            // ESC interrupts — abandon CSI
            this.state = VtState.Escape;
        }
    }

    private void ProcessCsiIntermediate(byte b)
    {
        if (b >= 0x20 && b <= 0x2F)
        {
            // Intermediate bytes — stay in state
        }
        else if (b >= 0x40 && b <= 0x7E)
        {
            // Final byte — ignore sequences with intermediates we don't handle
            this.state = VtState.Ground;
        }
        else if (b == 0x1B)
        {
            this.state = VtState.Escape;
        }
        else
        {
            this.state = VtState.Ground;
        }
    }

    private void ProcessOscString(byte b)
    {
        if (b == 0x07)
        {
            // BEL — string terminator
            this.DispatchOsc();
            this.state = VtState.Ground;
        }
        else if (b == 0x1B)
        {
            this.state = VtState.OscStringEsc;
        }
        else
        {
            this.oscString.Append((char)b);
        }
    }

    private void ProcessOscStringEsc(byte b)
    {
        if (b == 0x5C)
        {
            // '\' — ST
            this.DispatchOsc();
            this.state = VtState.Ground;
        }
        else
        {
            // ESC followed by something other than '\' — treat as new escape sequence
            this.state = VtState.Escape;
            this.ProcessEscape(b);
        }
    }

    private void EnterCsi()
    {
        this.parameters.Clear();
        this.subParameters.Clear();
        this.currentParam = 0;
        this.hasCurrentParam = false;
        this.currentSubParam = -1;
        this.inSubParam = false;
        this.privateMarker = false;
        this.state = VtState.CsiParam;
    }

    private void FinalizeParam()
    {
        this.parameters.Add(this.hasCurrentParam ? this.currentParam : 0);
        this.subParameters.Add(this.inSubParam ? this.currentSubParam : -1);
        this.currentParam = 0;
        this.hasCurrentParam = false;
        this.currentSubParam = -1;
        this.inSubParam = false;
    }

    private int GetParam(int index, int defaultValue)
    {
        if (index < this.parameters.Count && this.parameters[index] != 0)
        {
            return this.parameters[index];
        }

        return defaultValue;
    }

    private void DispatchCsi(char final)
    {
        switch (final)
        {
            case 'A': // CUU — cursor up
                this.buffer.MoveCursorUp(this.GetParam(0, 1));
                break;
            case 'B': // CUD — cursor down
                this.buffer.MoveCursorDown(this.GetParam(0, 1));
                break;
            case 'C': // CUF — cursor forward
                this.buffer.MoveCursorForward(this.GetParam(0, 1));
                break;
            case 'D': // CUB — cursor back
                this.buffer.MoveCursorBack(this.GetParam(0, 1));
                break;
            case 'G': // CHA — cursor character absolute (column)
                this.buffer.SetCursorPosition(this.buffer.CursorRow, this.GetParam(0, 1) - 1);
                break;
            case 'H': // CUP — cursor position
            case 'f':
                this.buffer.SetCursorPosition(this.GetParam(0, 1) - 1, this.GetParam(1, 1) - 1);
                break;
            case 'J': // ED — erase in display
                this.buffer.EraseInDisplay(this.GetParam(0, 0));
                break;
            case 'K': // EL — erase in line
                this.buffer.EraseInLine(this.GetParam(0, 0));
                break;
            case 'L': // IL — insert lines
                this.buffer.InsertLines(this.GetParam(0, 1));
                break;
            case 'M': // DL — delete lines
                this.buffer.DeleteLines(this.GetParam(0, 1));
                break;
            case 'P': // DCH — delete characters
                this.buffer.DeleteCharacters(this.GetParam(0, 1));
                break;
            case 'S': // SU — scroll up
                this.buffer.ScrollUp(this.GetParam(0, 1));
                break;
            case 'T': // SD — scroll down
                this.buffer.ScrollDown(this.GetParam(0, 1));
                break;
            case 'X': // ECH — erase characters
                this.buffer.EraseCharacters(this.GetParam(0, 1));
                break;
            case '@': // ICH — insert characters
                this.buffer.InsertCharacters(this.GetParam(0, 1));
                break;
            case 'd': // VPA — line position absolute (row)
                this.buffer.SetCursorPosition(this.GetParam(0, 1) - 1, this.buffer.CursorCol);
                break;
            case 'm': // SGR — select graphic rendition
                this.DispatchSgr();
                break;
            case 'r': // DECSTBM — set scrolling region
                int top = this.GetParam(0, 1) - 1;
                int bottom = this.GetParam(1, this.buffer.Rows) - 1;
                this.buffer.SetScrollRegion(top, bottom);
                break;
            case 'h': // SM / DECSET — set mode
                this.DispatchSetMode(true);
                break;
            case 'l': // RM / DECRST — reset mode
                this.DispatchSetMode(false);
                break;
            case 't': // Window manipulation — ignore
            case 'n': // DSR — device status report — ignore
                break;
        }
    }

    private void DispatchSetMode(bool enable)
    {
        if (!this.privateMarker)
        {
            return;
        }

        for (int i = 0; i < this.parameters.Count; i++)
        {
            switch (this.parameters[i])
            {
                case 25: // DECTCEM — cursor visibility
                    this.buffer.CursorVisible = enable;
                    break;
                case 1006: // SGR mouse mode
                    this.buffer.SgrMouseEnabled = enable;
                    break;
                case 1049: // Alternate screen buffer
                    if (enable)
                    {
                        this.buffer.SwitchToAlternateBuffer();
                    }
                    else
                    {
                        this.buffer.SwitchToMainBuffer();
                    }

                    break;
            }
        }
    }

    private void DispatchSgr()
    {
        if (this.parameters.Count == 0)
        {
            this.buffer.ResetAttributes();
            return;
        }

        for (int i = 0; i < this.parameters.Count; i++)
        {
            int p = this.parameters[i];
            switch (p)
            {
                case 0:
                    this.buffer.ResetAttributes();
                    break;
                case 1:
                    this.buffer.SetBold(true);
                    break;
                case 3:
                    this.buffer.SetItalic(true);
                    break;
                case 4:
                    if (i < this.subParameters.Count && this.subParameters[i] == 3)
                    {
                        this.buffer.SetUndercurl(true);
                    }
                    else
                    {
                        this.buffer.SetUnderline(true);
                    }

                    break;
                case 7:
                    this.buffer.SetReverse(true);
                    break;
                case 22:
                    this.buffer.SetBold(false);
                    break;
                case 23:
                    this.buffer.SetItalic(false);
                    break;
                case 24:
                    this.buffer.SetUnderline(false);
                    this.buffer.SetUndercurl(false);
                    break;
                case 27:
                    this.buffer.SetReverse(false);
                    break;

                // Standard foreground colors (30-37)
                case int n when n >= 30 && n <= 37:
                    this.buffer.SetForegroundColor(StandardColors[n - 30]);
                    break;

                case 38: // Extended foreground
                    i = this.ParseExtendedColor(i, true);
                    break;

                case 39: // Default foreground
                    this.buffer.SetDefaultForeground();
                    break;

                // Standard background colors (40-47)
                case int n when n >= 40 && n <= 47:
                    this.buffer.SetBackgroundColor(StandardColors[n - 40]);
                    break;

                case 48: // Extended background
                    i = this.ParseExtendedColor(i, false);
                    break;

                case 49: // Default background
                    this.buffer.SetDefaultBackground();
                    break;

                // Bright foreground colors (90-97)
                case int n when n >= 90 && n <= 97:
                    this.buffer.SetForegroundColor(BrightColors[n - 90]);
                    break;

                // Bright background colors (100-107)
                case int n when n >= 100 && n <= 107:
                    this.buffer.SetBackgroundColor(BrightColors[n - 100]);
                    break;
            }
        }
    }

    /// <summary>
    /// Parse extended color (38;5;N or 38;2;R;G;B) and return the updated parameter index.
    /// </summary>
    private int ParseExtendedColor(int index, bool isForeground)
    {
        if (index + 1 >= this.parameters.Count)
        {
            return index;
        }

        int mode = this.parameters[index + 1];
        if (mode == 5 && index + 2 < this.parameters.Count)
        {
            // 256-color: 38;5;N
            int colorIndex = Math.Clamp(this.parameters[index + 2], 0, 255);
            int color = Convert256Color(colorIndex);
            if (isForeground)
            {
                this.buffer.SetForegroundColor(color);
            }
            else
            {
                this.buffer.SetBackgroundColor(color);
            }

            return index + 2;
        }

        if (mode == 2 && index + 4 < this.parameters.Count)
        {
            // Truecolor: 38;2;R;G;B — pack as RGB
            int r = Math.Clamp(this.parameters[index + 2], 0, 255);
            int g = Math.Clamp(this.parameters[index + 3], 0, 255);
            int b = Math.Clamp(this.parameters[index + 4], 0, 255);
            int color = PackRgb(r, g, b);
            if (isForeground)
            {
                this.buffer.SetForegroundColor(color);
            }
            else
            {
                this.buffer.SetBackgroundColor(color);
            }

            return index + 4;
        }

        return index + 1;
    }

    private void DispatchOsc()
    {
        string text = this.oscString.ToString();
        int semicolonIndex = text.IndexOf(';');
        if (semicolonIndex < 0)
        {
            return;
        }

        string commandStr = text.Substring(0, semicolonIndex);
        string payload = text.Substring(semicolonIndex + 1);

        if (int.TryParse(commandStr, out int command))
        {
            switch (command)
            {
                case 0: // Set icon name and window title
                case 2: // Set window title
                    this.titleChanged?.Invoke(payload);
                    break;

                case 10: // Set/query foreground color
                    this.HandleOscColor(payload, isForeground: true);
                    break;

                case 11: // Set/query background color
                    this.HandleOscColor(payload, isForeground: false);
                    break;
            }
        }
    }

    private void HandleOscColor(string payload, bool isForeground)
    {
        if (payload == "?")
        {
            // Query: respond with current default color (stored as RGB).
            int color = isForeground ? this.buffer.DefaultForeground : this.buffer.DefaultBackground;
            byte r = (byte)((color >> 16) & 0xFF);
            byte g = (byte)((color >> 8) & 0xFF);
            byte b = (byte)(color & 0xFF);
            int oscCommand = isForeground ? 10 : 11;
            string response = $"\x1B]{oscCommand};rgb:{r:x2}{r:x2}/{g:x2}{g:x2}/{b:x2}{b:x2}\x1B\\";
            this.writeBack?.Invoke(Encoding.ASCII.GetBytes(response));
            return;
        }

        int parsed = ParseOscColorSpec(payload);
        if (parsed >= 0)
        {
            if (isForeground)
            {
                this.buffer.SetTerminalDefaultForeground(parsed);
            }
            else
            {
                this.buffer.SetTerminalDefaultBackground(parsed);
            }
        }
    }
}
