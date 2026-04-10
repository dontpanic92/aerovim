// <copyright file="PopupMenuOverlay.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Controls;

using AeroVim.Editor;
using AeroVim.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

/// <summary>
/// Floating popup completion menu overlay that renders Neovim's
/// <c>ext_popupmenu</c> events in an elegant acrylic panel.
/// </summary>
public partial class PopupMenuOverlay : UserControl
{
    /// <summary>
    /// Maximum width of the popup menu overlay in pixels.
    /// </summary>
    public const double MaxPopupWidth = 500;
    private const double ItemPaddingH = 8;
    private const double ItemPaddingV = 2;

    private readonly StackPanel itemsPanel;
    private readonly Border contentBorder;
    private readonly ExperimentalAcrylicBorder acrylicBorder;

    private string fontFamily = "Consolas";
    private double fontSize = 14;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopupMenuOverlay"/> class.
    /// </summary>
    public PopupMenuOverlay()
    {
        this.InitializeComponent();

        this.itemsPanel = this.FindControl<StackPanel>("ItemsPanel")!;
        this.contentBorder = this.FindControl<Border>("ContentBorder")!;
        this.acrylicBorder = this.FindControl<ExperimentalAcrylicBorder>("AcrylicBorder")!;

        this.Opacity = 0;
        this.IsVisible = false;

        AcrylicMaterialHelper.ApplyPlatformDefaults(this.acrylicBorder);
    }

    /// <summary>
    /// Updates the popup to display the given completion items at the
    /// specified anchor position. Shows the popup if not already visible.
    /// </summary>
    /// <param name="items">The completion menu items.</param>
    /// <param name="selected">The selected item index, or <c>-1</c> for none.</param>
    /// <param name="anchorX">The anchor X position in pixels (relative to editor).</param>
    /// <param name="anchorY">The anchor Y position in pixels (below the cursor line).</param>
    /// <param name="editorWidth">The editor width in pixels (for bounds clamping).</param>
    /// <param name="editorHeight">The editor height in pixels (for flip-above logic).</param>
    /// <param name="fgColor">The foreground (text) color as an RGB integer.</param>
    /// <param name="bgColor">The background color as an RGB integer.</param>
    /// <param name="anchorAbove">
    /// When <c>true</c>, positions the popup above <paramref name="anchorY"/>
    /// (for cmdline-anchored popups). When <c>false</c>, below it.
    /// </param>
    public void UpdatePopup(
        PopupMenuItem[] items,
        int selected,
        double anchorX,
        double anchorY,
        double editorWidth,
        double editorHeight,
        int fgColor,
        int bgColor,
        bool anchorAbove = false)
    {
        var fg = Helpers.GetAvaloniaColor(fgColor);
        var bg = Helpers.GetAvaloniaColor(bgColor);
        var borderBrush = new SolidColorBrush(Color.FromArgb(0x50, fg.R, fg.G, fg.B));
        var textBrush = new SolidColorBrush(fg);
        var selectedBg = new SolidColorBrush(fg);
        var selectedFg = new SolidColorBrush(bg);

        this.contentBorder.BorderBrush = borderBrush;
        AcrylicMaterialHelper.UpdateTint(this.acrylicBorder, bg);

        // Rebuild items
        this.itemsPanel.Children.Clear();
        var fontFam = new FontFamily(this.fontFamily);

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            bool isSelected = i == selected;

            var row = new Border
            {
                Background = isSelected ? selectedBg : Brushes.Transparent,
                Padding = new Thickness(ItemPaddingH, ItemPaddingV),
            };

            var grid = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"),
            };

            var wordBlock = new TextBlock
            {
                Text = item.Word,
                FontFamily = fontFam,
                FontSize = this.fontSize,
                Foreground = isSelected ? selectedFg : textBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(wordBlock, 0);

            grid.Children.Add(wordBlock);

            if (!string.IsNullOrEmpty(item.Kind))
            {
                var kindBlock = new TextBlock
                {
                    Text = item.Kind,
                    FontFamily = fontFam,
                    FontSize = this.fontSize * 0.9,
                    Foreground = isSelected ? selectedFg : new SolidColorBrush(Color.FromArgb(0x99, fg.R, fg.G, fg.B)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                };
                Grid.SetColumn(kindBlock, 1);
                grid.Children.Add(kindBlock);
            }

            row.Child = grid;
            this.itemsPanel.Children.Add(row);
        }

        this.MaxWidth = MaxPopupWidth;

        // Position: place below or above anchor depending on context
        double estimatedHeight = items.Length * (this.fontSize + (ItemPaddingV * 2) + 2);
        double y;

        if (anchorAbove)
        {
            // Place above the anchor point (bottom edge of popup at anchorY)
            y = anchorY - estimatedHeight;
            if (y < 0)
            {
                y = 0;
            }
        }
        else
        {
            y = anchorY;
            if (anchorY + estimatedHeight > editorHeight && anchorY > estimatedHeight)
            {
                y = anchorY - estimatedHeight - (this.fontSize * 1.4);
            }
        }

        double x = anchorX;
        if (x + MaxPopupWidth > editorWidth)
        {
            x = Math.Max(0, editorWidth - MaxPopupWidth);
        }

        this.Margin = new Thickness(x, y, 0, 0);

        if (!this.IsVisible)
        {
            this.IsVisible = true;
            this.Opacity = 1;
        }
    }

    /// <summary>
    /// Hides the popup menu overlay.
    /// </summary>
    public void HidePopup()
    {
        if (this.IsVisible)
        {
            this.Opacity = 0;
            Avalonia.Threading.DispatcherTimer.RunOnce(() => this.IsVisible = false, TimeSpan.FromMilliseconds(110));
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
}
