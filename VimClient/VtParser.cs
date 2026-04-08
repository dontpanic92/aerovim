// <copyright file="VtParser.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.VimClient;

using System.Text;
using AeroVim.Editor.Utilities;

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
    private readonly Func<string>? clipboardRead;
    private readonly Action<string>? clipboardWrite;
    private readonly List<int> parameters = new();
    private readonly List<int> subParameters = new();
    private readonly StringBuilder oscString = new();

    private VtState state = VtState.Ground;
    private bool privateMarker;
    private bool greaterThanMarker;
    private byte intermediateChar;

    private int currentParam;
    private bool hasCurrentParam;
    private int currentSubParam = -1;
    private bool inSubParam;

    // UTF-8 decoding state
    private int utf8BytesRemaining;
    private int utf8CodePoint;

    // Last printed character for REP (CSI b) support
    private int lastPrintedCodePoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="VtParser"/> class.
    /// </summary>
    /// <param name="buffer">The terminal buffer to update.</param>
    /// <param name="titleChanged">Callback invoked when the window title changes.</param>
    /// <param name="writeBack">Optional callback to write response bytes back to the PTY.</param>
    /// <param name="clipboardRead">Optional callback to read system clipboard text.</param>
    /// <param name="clipboardWrite">Optional callback to write text to the system clipboard.</param>
    public VtParser(
        TerminalBuffer buffer,
        Action<string> titleChanged,
        Action<byte[]>? writeBack = null,
        Func<string>? clipboardRead = null,
        Action<string>? clipboardWrite = null)
    {
        this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        this.titleChanged = titleChanged;
        this.writeBack = writeBack;
        this.clipboardRead = clipboardRead;
        this.clipboardWrite = clipboardWrite;
    }

    private enum VtState
    {
        Ground,
        Escape,
        EscapeIntermediate,
        EscapeCharsetG0,
        EscapeCharsetG1,
        EscapeCharsetG2,
        EscapeCharsetG3,
        CsiParam,
        CsiIntermediate,
        OscString,
        OscStringEsc,
        DcsString,
        DcsStringEsc,
        IgnoreString,
        IgnoreStringEsc,
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
                case VtState.EscapeIntermediate:
                    this.ProcessEscapeIntermediate(b);
                    break;
                case VtState.EscapeCharsetG0:
                    this.ProcessEscapeCharset(b, 0);
                    break;
                case VtState.EscapeCharsetG1:
                    this.ProcessEscapeCharset(b, 1);
                    break;
                case VtState.EscapeCharsetG2:
                    this.ProcessEscapeCharset(b, 2);
                    break;
                case VtState.EscapeCharsetG3:
                    this.ProcessEscapeCharset(b, 3);
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
                case VtState.DcsString:
                    this.ProcessDcsString(b);
                    break;
                case VtState.DcsStringEsc:
                    this.ProcessDcsStringEsc(b);
                    break;
                case VtState.IgnoreString:
                    this.ProcessIgnoreString(b);
                    break;
                case VtState.IgnoreStringEsc:
                    this.ProcessIgnoreStringEsc(b);
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
                    this.lastPrintedCodePoint = this.utf8CodePoint;
                    this.buffer.PutChar(this.utf8CodePoint);
                }

                return;
            }

            // Invalid continuation — reset and fall through
            this.utf8BytesRemaining = 0;
        }

        if (b >= 0x20 && b <= 0x7E)
        {
            this.lastPrintedCodePoint = b;
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
            case 0x0E: // SO — shift out (activate G1)
                this.buffer.ShiftOut();
                break;
            case 0x0F: // SI — shift in (activate G0)
                this.buffer.ShiftIn();
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
            case (byte)'P': // DCS — device control string
                this.state = VtState.DcsString;
                break;
            case (byte)'^': // PM — privacy message (consume and ignore)
            case (byte)'_': // APC — application program command (consume and ignore)
                this.state = VtState.IgnoreString;
                break;
            case (byte)'(': // G0 charset designation
                this.state = VtState.EscapeCharsetG0;
                break;
            case (byte)')': // G1 charset designation
                this.state = VtState.EscapeCharsetG1;
                break;
            case (byte)'*': // G2 charset designation
                this.state = VtState.EscapeCharsetG2;
                break;
            case (byte)'+': // G3 charset designation
                this.state = VtState.EscapeCharsetG3;
                break;
            case (byte)'#': // DEC private sequences (e.g. ESC # 8 DECALN)
                this.intermediateChar = (byte)'#';
                this.state = VtState.EscapeIntermediate;
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
            case (byte)'N': // SS2 — single shift G2
                this.buffer.SingleShift(2);
                this.state = VtState.Ground;
                break;
            case (byte)'O': // SS3 — single shift G3
                this.buffer.SingleShift(3);
                this.state = VtState.Ground;
                break;
            case (byte)'D': // IND — index
                this.buffer.LineFeed();
                this.state = VtState.Ground;
                break;
            case (byte)'E': // NEL — next line
                this.buffer.CarriageReturn();
                this.buffer.LineFeed();
                this.state = VtState.Ground;
                break;
            case (byte)'c': // RIS — full reset
                this.buffer.Reset();
                this.state = VtState.Ground;
                break;
            case (byte)'H': // HTS — set tab stop at current cursor column
                this.buffer.SetTabStopAtCursor();
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

    private void ProcessEscapeIntermediate(byte b)
    {
        if (this.intermediateChar == (byte)'#' && b == (byte)'8')
        {
            // DECALN — fill screen with 'E' for alignment test
            this.buffer.FillWithE();
        }

        this.state = VtState.Ground;
    }

    private void ProcessEscapeCharset(byte b, int gSet)
    {
        switch (b)
        {
            case (byte)'0': // DEC Special Graphics
                this.buffer.DesignateCharset(gSet, true);
                break;
            case (byte)'B': // ASCII
            default:
                this.buffer.DesignateCharset(gSet, false);
                break;
        }

        this.state = VtState.Ground;
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
        else if (b == (byte)'>')
        {
            this.greaterThanMarker = true;
        }
        else if (b >= 0x20 && b <= 0x2F)
        {
            this.FinalizeParam();
            this.intermediateChar = b;
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
            // Final byte — dispatch known sequences with intermediates
            this.DispatchCsiIntermediate((char)b);
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

    private void ProcessDcsString(byte b)
    {
        if (b == 0x07)
        {
            // BEL — string terminator (non-standard but common)
            this.state = VtState.Ground;
        }
        else if (b == 0x1B)
        {
            this.state = VtState.DcsStringEsc;
        }

        // Currently consume and ignore all DCS content
    }

    private void ProcessDcsStringEsc(byte b)
    {
        if (b == 0x5C)
        {
            // '\' — ST
            this.state = VtState.Ground;
        }
        else
        {
            this.state = VtState.Escape;
            this.ProcessEscape(b);
        }
    }

    private void ProcessIgnoreString(byte b)
    {
        if (b == 0x07)
        {
            this.state = VtState.Ground;
        }
        else if (b == 0x1B)
        {
            this.state = VtState.IgnoreStringEsc;
        }
    }

    private void ProcessIgnoreStringEsc(byte b)
    {
        if (b == 0x5C)
        {
            this.state = VtState.Ground;
        }
        else
        {
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
        this.greaterThanMarker = false;
        this.intermediateChar = 0;
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
            case '`': // HPA — horizontal position absolute (like CHA)
                this.buffer.SetCursorPosition(this.buffer.CursorRow, this.GetParam(0, 1) - 1);
                break;
            case 'a': // HPR — horizontal position relative (like CUF)
                this.buffer.MoveCursorForward(this.GetParam(0, 1));
                break;
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
            case 'E': // CNL — cursor next line
                this.buffer.MoveCursorDown(this.GetParam(0, 1));
                this.buffer.CarriageReturn();
                break;
            case 'F': // CPL — cursor preceding line
                this.buffer.MoveCursorUp(this.GetParam(0, 1));
                this.buffer.CarriageReturn();
                break;
            case 'G': // CHA — cursor character absolute (column)
                this.buffer.SetCursorPosition(this.buffer.CursorRow, this.GetParam(0, 1) - 1);
                break;
            case 'H': // CUP — cursor position
            case 'f':
                this.buffer.SetCursorPosition(this.GetParam(0, 1) - 1, this.GetParam(1, 1) - 1);
                break;
            case 'I': // CHT — cursor horizontal tabulation
                {
                    int n = this.GetParam(0, 1);
                    for (int i = 0; i < n; i++)
                    {
                        this.buffer.AdvanceToNextTabStop();
                    }
                }

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
            case 'Z': // CBT — cursor backward tabulation
                {
                    int n = this.GetParam(0, 1);
                    for (int i = 0; i < n; i++)
                    {
                        this.buffer.BackTab();
                    }
                }

                break;
            case '@': // ICH — insert characters
                this.buffer.InsertCharacters(this.GetParam(0, 1));
                break;
            case 'b': // REP — repeat preceding graphic character
                if (this.lastPrintedCodePoint != 0)
                {
                    int count = this.GetParam(0, 1);
                    for (int i = 0; i < count; i++)
                    {
                        this.buffer.PutChar(this.lastPrintedCodePoint);
                    }
                }

                break;
            case 'd': // VPA — line position absolute (row)
                this.buffer.SetCursorPosition(this.GetParam(0, 1) - 1, this.buffer.CursorCol);
                break;
            case 'e': // VPR — vertical position relative (like CUD)
                this.buffer.MoveCursorDown(this.GetParam(0, 1));
                break;
            case 'g': // TBC — tab clear
                {
                    int ps = this.GetParam(0, 0);
                    if (ps == 0)
                    {
                        this.buffer.ClearTabStopAtCursor();
                    }
                    else if (ps == 3)
                    {
                        this.buffer.ClearAllTabStops();
                    }
                }

                break;
            case 'm': // SGR — select graphic rendition
                if (!this.privateMarker && !this.greaterThanMarker)
                {
                    this.DispatchSgr();
                }

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
            case 'p' when this.greaterThanMarker: // XTSMPOINTER — set pointer mode
                this.buffer.PointerMode = this.GetParam(0, 1);
                break;
            case 't': // Window manipulation — ignore
                break;
            case 'n': // DSR — device status report
                this.DispatchDsr();
                break;
            case 'c': // DA1 / DA2 — device attributes
                if (!this.privateMarker && !this.greaterThanMarker)
                {
                    // DA1: Respond as VT520 with ANSI color support
                    this.writeBack?.Invoke(Encoding.ASCII.GetBytes("\x1B[?64;22c"));
                }
                else if (this.greaterThanMarker && !this.privateMarker)
                {
                    // DA2: Secondary device attributes — report as VT100, version 10, no hardware options
                    this.writeBack?.Invoke(Encoding.ASCII.GetBytes("\x1B[>1;10;0c"));
                }

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
                case 1: // DECCKM — application cursor keys
                    this.buffer.ApplicationCursorKeys = enable;
                    break;
                case 5: // DECSCNM — screen reverse video
                    this.buffer.ReverseVideo = enable;
                    break;
                case 6: // DECOM — origin mode
                    this.buffer.OriginMode = enable;
                    this.buffer.SetCursorPosition(0, 0);
                    break;
                case 7: // DECAWM — auto-wrap mode
                    this.buffer.AutoWrap = enable;
                    break;
                case 12: // Cursor blink toggle
                    this.buffer.RequestedCursorBlinking = enable
                        ? CursorBlinking.BlinkOn
                        : CursorBlinking.BlinkOff;
                    break;
                case 25: // DECTCEM — cursor visibility
                    this.buffer.CursorVisible = enable;
                    break;
                case 1000: // Normal mouse tracking
                    this.buffer.MouseTrackingMode = enable ? MouseTrackingMode.Normal : MouseTrackingMode.None;
                    break;
                case 1002: // Button-event mouse tracking
                    this.buffer.MouseTrackingMode = enable ? MouseTrackingMode.ButtonEvent : MouseTrackingMode.None;
                    break;
                case 1003: // Any-event mouse tracking
                    this.buffer.MouseTrackingMode = enable ? MouseTrackingMode.AnyEvent : MouseTrackingMode.None;
                    break;
                case 1004: // Focus events
                    this.buffer.FocusEventsEnabled = enable;
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
                case 47: // Alternate screen (without cursor save/restore)
                    if (enable)
                    {
                        this.buffer.SwitchToAlternateBuffer();
                    }
                    else
                    {
                        this.buffer.SwitchToMainBuffer();
                    }

                    break;
                case 1047: // Alternate screen (clears alt on switch)
                    if (enable)
                    {
                        this.buffer.SwitchToAlternateBuffer();
                    }
                    else
                    {
                        this.buffer.SwitchToMainBuffer();
                    }

                    break;
                case 1048: // Save/restore cursor (like DECSC/DECRC)
                    if (enable)
                    {
                        this.buffer.SaveCursor();
                    }
                    else
                    {
                        this.buffer.RestoreCursor();
                    }

                    break;
                case 2004: // Bracketed paste
                    this.buffer.BracketedPasteEnabled = enable;
                    break;
                case 2026: // Synchronized output
                    this.buffer.SynchronizedOutput = enable;
                    break;
            }
        }
    }

    private void DispatchDsr()
    {
        if (this.privateMarker)
        {
            return;
        }

        int ps = this.GetParam(0, 0);
        switch (ps)
        {
            case 5: // Status report — respond "terminal OK"
                this.writeBack?.Invoke(Encoding.ASCII.GetBytes("\x1B[0n"));
                break;
            case 6: // Cursor position report
                string cpr = $"\x1B[{this.buffer.CursorRow + 1};{this.buffer.CursorCol + 1}R";
                this.writeBack?.Invoke(Encoding.ASCII.GetBytes(cpr));
                break;
        }
    }

    private void DispatchCsiIntermediate(char final)
    {
        // DECSCUSR — CSI Ps SP q — set cursor style
        if (this.intermediateChar == 0x20 && final == 'q')
        {
            int ps = this.GetParam(0, 0);
            switch (ps)
            {
                case 0: // Default — reset to terminal's default cursor
                    this.buffer.RequestedCursorShape = null;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOff;
                    break;
                case 1: // Blinking block
                    this.buffer.RequestedCursorShape = CursorShape.Block;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOn;
                    break;
                case 2: // Steady block
                    this.buffer.RequestedCursorShape = CursorShape.Block;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOff;
                    break;
                case 3: // Blinking underline
                    this.buffer.RequestedCursorShape = CursorShape.Horizontal;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOn;
                    break;
                case 4: // Steady underline
                    this.buffer.RequestedCursorShape = CursorShape.Horizontal;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOff;
                    break;
                case 5: // Blinking bar (vertical)
                    this.buffer.RequestedCursorShape = CursorShape.Vertical;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOn;
                    break;
                case 6: // Steady bar (vertical)
                    this.buffer.RequestedCursorShape = CursorShape.Vertical;
                    this.buffer.RequestedCursorBlinking = CursorBlinking.BlinkOff;
                    break;
            }
        }
        else if (this.intermediateChar == (byte)'$' && final == 'p')
        {
            // DECRQM — request mode
            int ps = this.GetParam(0, 0);
            if (this.privateMarker)
            {
                int status = this.GetPrivateModeStatus(ps);
                string response = $"\x1B[?{ps};{status}$y";
                this.writeBack?.Invoke(Encoding.ASCII.GetBytes(response));
            }
        }
    }

    private int GetPrivateModeStatus(int mode)
    {
        return mode switch
        {
            1 => this.buffer.ApplicationCursorKeys ? 1 : 2,
            5 => this.buffer.ReverseVideo ? 1 : 2,
            6 => this.buffer.OriginMode ? 1 : 2,
            7 => this.buffer.AutoWrap ? 1 : 2,
            12 => this.buffer.RequestedCursorBlinking == CursorBlinking.BlinkOn ? 1 : 2,
            25 => this.buffer.CursorVisible ? 1 : 2,
            47 or 1047 or 1049 => 2,
            1000 => this.buffer.MouseTrackingMode == MouseTrackingMode.Normal ? 1 : 2,
            1002 => this.buffer.MouseTrackingMode == MouseTrackingMode.ButtonEvent ? 1 : 2,
            1003 => this.buffer.MouseTrackingMode == MouseTrackingMode.AnyEvent ? 1 : 2,
            1004 => this.buffer.FocusEventsEnabled ? 1 : 2,
            1006 => this.buffer.SgrMouseEnabled ? 1 : 2,
            2004 => this.buffer.BracketedPasteEnabled ? 1 : 2,
            2026 => this.buffer.SynchronizedOutput ? 1 : 2,
            _ => 0, // unknown mode
        };
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
                case 2:
                    this.buffer.SetDim(true);
                    break;
                case 3:
                    this.buffer.SetItalic(true);
                    break;
                case 4:
                    int sub4 = (i < this.subParameters.Count) ? this.subParameters[i] : -1;
                    switch (sub4)
                    {
                        case 0: // 4:0 — underline/undercurl off
                            this.buffer.SetUnderline(false);
                            this.buffer.SetUndercurl(false);
                            break;
                        case 3: // 4:3 — curly underline (undercurl)
                            this.buffer.SetUnderline(false);
                            this.buffer.SetUndercurl(true);
                            break;
                        default: // bare 4, 4:1, 4:2, 4:4, 4:5 — single underline
                            this.buffer.SetUndercurl(false);
                            this.buffer.SetUnderline(true);
                            break;
                    }

                    break;
                case 7:
                    this.buffer.SetReverse(true);
                    break;
                case 5: // Slow blink
                case 6: // Rapid blink
                    this.buffer.SetBlink(true);
                    break;
                case 8:
                    this.buffer.SetHidden(true);
                    break;
                case 9:
                    this.buffer.SetStrikethrough(true);
                    break;
                case 21: // Double underline — treat as underline
                    this.buffer.SetUnderline(true);
                    break;
                case 22:
                    this.buffer.SetBold(false);
                    this.buffer.SetDim(false);
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
                case 25:
                    this.buffer.SetBlink(false);
                    break;
                case 28:
                    this.buffer.SetHidden(false);
                    break;
                case 29:
                    this.buffer.SetStrikethrough(false);
                    break;
                case 53:
                    this.buffer.SetOverline(true);
                    break;
                case 55:
                    this.buffer.SetOverline(false);
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

                case 58: // Extended special (underline) color
                    i = this.ParseExtendedColor(i, isForeground: false, isSpecial: true);
                    break;

                case 59: // Default special (underline) color
                    this.buffer.SetDefaultSpecial();
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
    private int ParseExtendedColor(int index, bool isForeground, bool isSpecial = false)
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
            this.ApplyExtendedColor(color, isForeground, isSpecial);
            return index + 2;
        }

        if (mode == 2 && index + 4 < this.parameters.Count)
        {
            // Truecolor: 38;2;R;G;B — pack as RGB
            int r = Math.Clamp(this.parameters[index + 2], 0, 255);
            int g = Math.Clamp(this.parameters[index + 3], 0, 255);
            int b = Math.Clamp(this.parameters[index + 4], 0, 255);
            int color = PackRgb(r, g, b);
            this.ApplyExtendedColor(color, isForeground, isSpecial);
            return index + 4;
        }

        return index + 1;
    }

    private void ApplyExtendedColor(int color, bool isForeground, bool isSpecial)
    {
        if (isSpecial)
        {
            this.buffer.SetSpecialColor(color);
        }
        else if (isForeground)
        {
            this.buffer.SetForegroundColor(color);
        }
        else
        {
            this.buffer.SetBackgroundColor(color);
        }
    }

    private void DispatchOsc()
    {
        string text = this.oscString.ToString();
        int semicolonIndex = text.IndexOf(';');

        // OSC commands without a payload (e.g. OSC 104 ; ST to reset all palette)
        if (semicolonIndex < 0)
        {
            if (int.TryParse(text, out int bareCommand))
            {
                switch (bareCommand)
                {
                    case 104: // Reset all palette colors
                        this.buffer.ResetPaletteColors();
                        break;
                    case 110: // Reset default foreground
                        this.buffer.ResetTerminalDefaultForeground();
                        break;
                    case 111: // Reset default background
                        this.buffer.ResetTerminalDefaultBackground();
                        break;
                    case 112: // Reset cursor color
                        this.buffer.ResetTerminalCursorColor();
                        break;
                }
            }

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

                case 4: // Set/query 256-color palette entry
                    this.HandleOscPalette(payload);
                    break;

                case 10: // Set/query foreground color
                    this.HandleOscColor(payload, isForeground: true);
                    break;

                case 11: // Set/query background color
                    this.HandleOscColor(payload, isForeground: false);
                    break;

                case 12: // Set/query cursor color
                    this.HandleOscCursorColor(payload);
                    break;

                case 22: // Set pointer cursor shape
                    this.buffer.PointerShape = payload;
                    break;

                case 52: // Clipboard access
                    this.HandleOscClipboard(payload);
                    break;

                case 104: // Reset palette color(s)
                    this.HandleOscResetPalette(payload);
                    break;

                case 110: // Reset default foreground
                    this.buffer.ResetTerminalDefaultForeground();
                    break;

                case 111: // Reset default background
                    this.buffer.ResetTerminalDefaultBackground();
                    break;

                case 112: // Reset cursor color
                    this.buffer.ResetTerminalCursorColor();
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
            int oscCommand = isForeground ? 10 : 11;
            this.WriteBackOscColorResponse(oscCommand, color);
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

    private void HandleOscCursorColor(string payload)
    {
        if (payload == "?")
        {
            this.WriteBackOscColorResponse(12, this.buffer.CursorColor);
            return;
        }

        int parsed = ParseOscColorSpec(payload);
        if (parsed >= 0)
        {
            this.buffer.SetTerminalCursorColor(parsed);
        }
    }

    private void HandleOscPalette(string payload)
    {
        // Format: index;spec  or  index;?  (may repeat: index;spec;index;spec)
        string[] parts = payload.Split(';');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (int.TryParse(parts[i], out int index) && index >= 0 && index <= 255)
            {
                if (parts[i + 1] == "?")
                {
                    int color = this.buffer.GetPaletteColor(index);
                    byte r = (byte)((color >> 16) & 0xFF);
                    byte g = (byte)((color >> 8) & 0xFF);
                    byte b = (byte)(color & 0xFF);
                    string response = $"\x1B]4;{index};rgb:{r:x2}{r:x2}/{g:x2}{g:x2}/{b:x2}{b:x2}\x1B\\";
                    this.writeBack?.Invoke(Encoding.ASCII.GetBytes(response));
                }
                else
                {
                    int parsed = ParseOscColorSpec(parts[i + 1]);
                    if (parsed >= 0)
                    {
                        this.buffer.SetPaletteColor(index, parsed);
                    }
                }
            }
        }
    }

    private void HandleOscResetPalette(string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            this.buffer.ResetPaletteColors();
            return;
        }

        // Format: index[;index...] — reset specific entries
        string[] parts = payload.Split(';');
        foreach (string part in parts)
        {
            if (int.TryParse(part, out int index) && index >= 0 && index <= 255)
            {
                this.buffer.ResetPaletteColor(index);
            }
        }
    }

    private void WriteBackOscColorResponse(int oscCommand, int color)
    {
        byte r = (byte)((color >> 16) & 0xFF);
        byte g = (byte)((color >> 8) & 0xFF);
        byte b = (byte)(color & 0xFF);
        string response = $"\x1B]{oscCommand};rgb:{r:x2}{r:x2}/{g:x2}{g:x2}/{b:x2}{b:x2}\x1B\\";
        this.writeBack?.Invoke(Encoding.ASCII.GetBytes(response));
    }

    private void HandleOscClipboard(string payload)
    {
        // Format: selection;base64-data  (selection is e.g. "c" for clipboard, "p" for primary)
        int semicolonIndex = payload.IndexOf(';');
        if (semicolonIndex < 0)
        {
            return;
        }

        string selection = payload.Substring(0, semicolonIndex);
        string data = payload.Substring(semicolonIndex + 1);

        if (data == "?")
        {
            // Query clipboard
            if (this.clipboardRead is not null && this.writeBack is not null)
            {
                string text = this.clipboardRead();
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                string response = $"\x1B]52;{selection};{base64}\x1B\\";
                this.writeBack(Encoding.UTF8.GetBytes(response));
            }
        }
        else
        {
            // Set clipboard
            if (this.clipboardWrite is not null)
            {
                try
                {
                    byte[] decoded = Convert.FromBase64String(data);
                    string text = Encoding.UTF8.GetString(decoded);
                    this.clipboardWrite(text);
                }
                catch (FormatException)
                {
                    // Invalid base64 — ignore
                }
            }
        }
    }
}
