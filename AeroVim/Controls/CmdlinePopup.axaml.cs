// <copyright file="CmdlinePopup.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using System.Text;
using AeroVim.Editor;
using AeroVim.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

/// <summary>
/// Floating command-line popup overlay that renders Neovim's
/// <c>ext_cmdline</c> events in an elegant acrylic panel.
/// </summary>
public partial class CmdlinePopup : UserControl
{
    private readonly TextBlock prefixText;
    private readonly TextBlock contentText;
    private readonly Border cursorCaret;
    private readonly Border contentBorder;
    private readonly ExperimentalAcrylicBorder acrylicBorder;

    private DispatcherTimer? cursorBlinkTimer;
    private bool cursorVisible = true;
    private string fontFamily = "Consolas";
    private double fontSize = 14;

    /// <summary>
    /// Initializes a new instance of the <see cref="CmdlinePopup"/> class.
    /// </summary>
    public CmdlinePopup()
    {
        this.InitializeComponent();

        this.prefixText = this.FindControl<TextBlock>("PrefixText")!;
        this.contentText = this.FindControl<TextBlock>("ContentText")!;
        this.cursorCaret = this.FindControl<Border>("CursorCaret")!;
        this.contentBorder = this.FindControl<Border>("ContentBorder")!;
        this.acrylicBorder = this.FindControl<ExperimentalAcrylicBorder>("AcrylicBorder")!;

        this.Opacity = 0;
        this.IsVisible = false;

        AcrylicMaterialHelper.ApplyPlatformDefaults(this.acrylicBorder);
    }

    /// <summary>
    /// Updates the popup to display the current command-line state.
    /// Shows the popup if it is not already visible.
    /// </summary>
    /// <param name="cmdline">The command-line state from Neovim.</param>
    /// <param name="fgColor">The foreground (text) color as an RGB integer.</param>
    /// <param name="bgColor">The background color as an RGB integer.</param>
    public void UpdateCmdline(CmdlineState cmdline, int fgColor, int bgColor)
    {
        var fg = Helpers.GetAvaloniaColor(fgColor);
        var bgTint = Helpers.GetAvaloniaColor(bgColor);
        var borderBrush = new SolidColorBrush(Color.FromArgb(0x50, fg.R, fg.G, fg.B));
        var textBrush = new SolidColorBrush(fg);

        this.contentBorder.BorderBrush = borderBrush;
        this.prefixText.Foreground = textBrush;
        this.contentText.Foreground = textBrush;
        this.cursorCaret.Background = textBrush;

        // Update acrylic tint to match editor background
        AcrylicMaterialHelper.UpdateTint(this.acrylicBorder, bgTint);

        // Prefix character
        this.prefixText.Text = cmdline.FirstChar;

        // Content text
        string text = JoinCmdlineContent(cmdline.Content);
        this.contentText.Text = text;

        // Apply font
        var fontFam = new FontFamily(this.fontFamily);
        this.prefixText.FontFamily = fontFam;
        this.contentText.FontFamily = fontFam;
        this.prefixText.FontSize = this.fontSize;
        this.contentText.FontSize = this.fontSize;

        // Position cursor
        this.PositionCursor(text, cmdline.CursorPos);

        // Show
        if (!this.IsVisible)
        {
            this.IsVisible = true;
            this.Opacity = 1;
            this.StartCursorBlink();
        }
    }

    /// <summary>
    /// Hides the command-line popup with a fade-out transition.
    /// </summary>
    public void HideCmdline()
    {
        if (this.IsVisible)
        {
            this.StopCursorBlink();
            this.Opacity = 0;

            // Hide after the transition completes
            DispatcherTimer.RunOnce(() => this.IsVisible = false, TimeSpan.FromMilliseconds(160));
        }
    }

    /// <summary>
    /// Updates the font used by the popup to match the editor font.
    /// </summary>
    /// <param name="family">The font family name.</param>
    /// <param name="size">The font size in device-independent pixels.</param>
    public void SetFont(string family, double size)
    {
        this.fontFamily = family;
        this.fontSize = size;
    }

    private static string JoinCmdlineContent(IList<(int HlId, string Text)> content)
    {
        if (content.Count == 0)
        {
            return string.Empty;
        }

        if (content.Count == 1)
        {
            return content[0].Text;
        }

        var sb = new StringBuilder();
        foreach (var (_, text) in content)
        {
            sb.Append(text);
        }

        return sb.ToString();
    }

    private static int BytePosToCharIndex(string text, int bytePos)
    {
        if (string.IsNullOrEmpty(text) || bytePos <= 0)
        {
            return 0;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        if (bytePos >= bytes.Length)
        {
            return text.Length;
        }

        // Decode byte-by-byte to find the character boundary
        string prefix = Encoding.UTF8.GetString(bytes, 0, bytePos);
        return prefix.Length;
    }

    private void PositionCursor(string text, int bytePos)
    {
        // Convert byte position to character index
        int charIndex = BytePosToCharIndex(text, bytePos);

        // Use WidthIncludingTrailingWhitespace so trailing spaces are counted.
        var fontFam = new FontFamily(this.fontFamily);
        var typeface = new Typeface(fontFam);
        var formattedPrefix = new FormattedText(
            text.Substring(0, Math.Min(charIndex, text.Length)),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            this.fontSize,
            Brushes.Transparent);

        double cursorX = formattedPrefix.WidthIncludingTrailingWhitespace;
        double caretHeight = Math.Ceiling(this.fontSize * 1.25);
        this.cursorCaret.Height = caretHeight;
        this.cursorCaret.Margin = new Thickness(cursorX, 0, 0, 0);
        this.cursorCaret.IsVisible = true;
    }

    private void StartCursorBlink()
    {
        this.StopCursorBlink();
        this.cursorVisible = true;
        this.cursorCaret.Opacity = 1;

        this.cursorBlinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(530),
        };
        this.cursorBlinkTimer.Tick += (_, _) =>
        {
            this.cursorVisible = !this.cursorVisible;
            this.cursorCaret.Opacity = this.cursorVisible ? 1 : 0;
        };
        this.cursorBlinkTimer.Start();
    }

    private void StopCursorBlink()
    {
        this.cursorBlinkTimer?.Stop();
        this.cursorBlinkTimer = null;
    }
}
