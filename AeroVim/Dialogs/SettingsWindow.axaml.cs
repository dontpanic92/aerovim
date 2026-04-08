// <copyright file="SettingsWindow.axaml.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Dialogs;

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using AeroVim.Editor.Utilities;
using AeroVim.Services;
using AeroVim.Utilities;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

/// <summary>
/// Interaction logic for SettingsWindow.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly IReadOnlyList<string> currentGuiFontNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// Used by the XAML designer; runtime code should use
    /// <see cref="SettingsWindow(AppSettings, string?, IReadOnlyList{string}?)"/>.
    /// </summary>
    public SettingsWindow()
        : this(AppSettings.Default, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWindow"/> class.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <param name="promptText">Text for prompt.</param>
    /// <param name="guiFontNames">The currently resolved Neovim guifont names, if available.</param>
    public SettingsWindow(AppSettings settings, string? promptText, IReadOnlyList<string>? guiFontNames = null)
    {
        this.settings = settings;
        this.currentGuiFontNames = guiFontNames ?? Array.Empty<string>();
        this.InitializeComponent();

        if (!string.IsNullOrWhiteSpace(promptText))
        {
            var promptLabel = this.FindControl<TextBlock>("PromptLabel")!;
            promptLabel.Text = promptText;
            promptLabel.IsVisible = true;
        }

        this.LoadSettingsToUi();
        this.LoadAboutInfo();

        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        fontListBox.SelectionChanged += (s, e) => this.UpdateFontButtonStates();
        this.UpdateFontButtonStates();

        var opacitySlider = this.FindControl<Slider>("OpacitySlider")!;
        opacitySlider.PropertyChanged += (s, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                this.FindControl<TextBlock>("OpacityLabel")!.Text = opacitySlider.Value.ToString("F2");
                this.settings.BackgroundOpacity = opacitySlider.Value;
            }
        };

        // Default to General page
        this.FindControl<ListBox>("PageListBox")!.SelectedIndex = 0;

        this.Closing += this.OnWindowClosing;
    }

    /// <summary>
    /// Reason of closing the window.
    /// </summary>
    public enum Result
    {
        /// <summary>
        /// Window is not closed yet.
        /// </summary>
        NotClosed,

        /// <summary>
        /// Window closed due to Ok button clicked.
        /// </summary>
        Ok,

        /// <summary>
        /// Window closed due to Cancel button clicked.
        /// </summary>
        Cancel,
    }

    /// <summary>
    /// Gets the reason of closing the window.
    /// </summary>
    public Result CloseReason { get; private set; } = Result.NotClosed;

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string? GetRawFontEntry(object? item)
    {
        if (item is FontPriorityItem sentinel)
        {
            return sentinel.Sentinel;
        }

        if (item is string fontName)
        {
            return fontName;
        }

        return null;
    }

    private void ShowPage(int index)
    {
        this.FindControl<StackPanel>("GeneralPage")!.IsVisible = index == 0;
        this.FindControl<StackPanel>("AppearancePage")!.IsVisible = index == 1;
        this.FindControl<StackPanel>("ShellIntegrationPage")!.IsVisible = index == 2;
        this.FindControl<StackPanel>("AboutPage")!.IsVisible = index == 3;
    }

    private void PageListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        if (listBox is not null)
        {
            this.ShowPage(listBox.SelectedIndex);
        }
    }

    private void LoadAboutInfo()
    {
        var informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var versionText = informationalVersion ?? "Version unknown";
        if (versionText.Split('+') is [var version, var build])
        {
            versionText = $"{version.Trim()} build {build[..7]}";
        }

        this.FindControl<TextBlock>("VersionLabel")!.Text = versionText;
    }

    private void GitHubLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OpenUrl("https://github.com/dontpanic92/aerovim");
    }

    private void IssuesLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OpenUrl("https://github.com/dontpanic92/aerovim/issues");
    }

    private void LoadSettingsToUi()
    {
        this.FindControl<TextBox>("NeovimPathBox")!.Text = this.settings.NeovimPath;
        this.FindControl<TextBox>("VimPathBox")!.Text = this.settings.VimPath;

        var editorTypeCombo = this.FindControl<ComboBox>("EditorTypeCombo")!;
        editorTypeCombo.SelectedIndex = (int)this.settings.EditorType;
        this.UpdatePathPanelVisibility();

        editorTypeCombo.SelectionChanged += (s, e) => this.UpdatePathPanelVisibility();

        // Font priority list
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        foreach (var entry in this.settings.FallbackFonts)
        {
            fontListBox.Items.Add(this.CreateFontListDisplayItem(entry));
        }

        this.FindControl<CheckBox>("LigatureCheckBox")!.IsChecked = this.settings.EnableLigature;

        var blurBehindCheckBox = this.FindControl<CheckBox>("BlurBehindCheckBox")!;
        blurBehindCheckBox.IsChecked = this.settings.EnableBlurBehind;

        var transparentRadio = this.FindControl<RadioButton>("TransparentRadio")!;
        transparentRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.TransparentAvailable();
        transparentRadio.IsChecked = this.settings.BlurType == 3 && this.settings.EnableBlurBehind;

        var gaussianRadio = this.FindControl<RadioButton>("GaussianRadio")!;
        gaussianRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.GaussianBlurAvailable();
        gaussianRadio.IsChecked = this.settings.BlurType == 0 && this.settings.EnableBlurBehind;

        var acrylicRadio = this.FindControl<RadioButton>("AcrylicRadio")!;
        acrylicRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.AcrylicBlurAvailable();
        acrylicRadio.IsChecked = this.settings.BlurType == 1 && this.settings.EnableBlurBehind;

        var micaRadio = this.FindControl<RadioButton>("MicaRadio")!;
        micaRadio.IsEnabled = this.settings.EnableBlurBehind && Helpers.MicaAvailable();
        micaRadio.IsChecked = this.settings.BlurType == 2 && this.settings.EnableBlurBehind;

        var opacitySlider = this.FindControl<Slider>("OpacitySlider")!;
        opacitySlider.Value = this.settings.BackgroundOpacity;
        opacitySlider.IsEnabled = this.settings.EnableBlurBehind;

        this.FindControl<TextBlock>("OpacityLabel")!.Text = this.settings.BackgroundOpacity.ToString("F2");

        blurBehindCheckBox.IsCheckedChanged += (s, e) =>
        {
            bool isChecked = blurBehindCheckBox.IsChecked == true;
            transparentRadio.IsEnabled = isChecked && Helpers.TransparentAvailable();
            gaussianRadio.IsEnabled = isChecked && Helpers.GaussianBlurAvailable();
            acrylicRadio.IsEnabled = isChecked && Helpers.AcrylicBlurAvailable();
            micaRadio.IsEnabled = isChecked && Helpers.MicaAvailable();
            opacitySlider.IsEnabled = isChecked;
            this.settings.EnableBlurBehind = isChecked;
        };

        // Live preview: apply blur type changes immediately.
        transparentRadio.IsCheckedChanged += (s, e) =>
        {
            if (transparentRadio.IsChecked == true)
            {
                this.settings.BlurType = 3;
            }
        };
        gaussianRadio.IsCheckedChanged += (s, e) =>
        {
            if (gaussianRadio.IsChecked == true)
            {
                this.settings.BlurType = 0;
            }
        };
        acrylicRadio.IsCheckedChanged += (s, e) =>
        {
            if (acrylicRadio.IsChecked == true)
            {
                this.settings.BlurType = 1;
            }
        };
        micaRadio.IsCheckedChanged += (s, e) =>
        {
            if (micaRadio.IsChecked == true)
            {
                this.settings.BlurType = 2;
            }
        };

        // Live preview: apply ligature changes immediately.
        var ligatureCheckBox = this.FindControl<CheckBox>("LigatureCheckBox")!;
        ligatureCheckBox.IsCheckedChanged += (s, e) =>
        {
            this.settings.EnableLigature = ligatureCheckBox.IsChecked == true;
        };

        // Shell Integration page
        this.FindControl<CheckBox>("DragDropCheckBox")!.IsChecked = this.settings.EnableDragDrop;

        // Only show Windows shell group on Windows.
        this.FindControl<Avalonia.Controls.Control>("WindowsShellGroup")!.IsVisible =
            ShellIntegrationService.IsSupported;

        this.PopulateFileAssociationList();
        this.RefreshShellIntegrationStatus();
    }

    private void SaveUiToSettings()
    {
        this.settings.NeovimPath = this.FindControl<TextBox>("NeovimPathBox")!.Text ?? string.Empty;
        this.settings.VimPath = this.FindControl<TextBox>("VimPathBox")!.Text ?? string.Empty;
        this.settings.EditorType = (EditorType)this.FindControl<ComboBox>("EditorTypeCombo")!.SelectedIndex;
        this.settings.EnableLigature = this.FindControl<CheckBox>("LigatureCheckBox")!.IsChecked == true;
        this.settings.EnableBlurBehind = this.FindControl<CheckBox>("BlurBehindCheckBox")!.IsChecked == true;
        this.settings.BackgroundOpacity = this.FindControl<Slider>("OpacitySlider")!.Value;

        // Font priority list
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        var fonts = new List<string>();
        foreach (var item in fontListBox.Items)
        {
            string? raw = GetRawFontEntry(item);
            if (raw is not null)
            {
                fonts.Add(raw);
            }
        }

        this.settings.FallbackFonts = fonts;

        if (this.FindControl<RadioButton>("TransparentRadio")!.IsChecked == true)
        {
            this.settings.BlurType = 3;
        }
        else if (this.FindControl<RadioButton>("GaussianRadio")!.IsChecked == true)
        {
            this.settings.BlurType = 0;
        }
        else if (this.FindControl<RadioButton>("AcrylicRadio")!.IsChecked == true)
        {
            this.settings.BlurType = 1;
        }
        else if (this.FindControl<RadioButton>("MicaRadio")!.IsChecked == true)
        {
            this.settings.BlurType = 2;
        }

        // Shell Integration — drag-and-drop is the only save-on-close setting.
        // Context menu and file associations are applied immediately via buttons.
        this.settings.EnableDragDrop = this.FindControl<CheckBox>("DragDropCheckBox")!.IsChecked == true;

        // Persist the extension list so it survives restart.
        this.settings.FileAssociationExtensions = this.GetExtensionListFromUi();
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        this.CloseReason = Result.Ok;
        this.Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        this.CloseReason = Result.Cancel;
        this.Close();
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var fileTypeFilters = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                new FilePickerFileType("Executable Files") { Patterns = ["*.exe"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
            }
            : new[]
            {
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            };

        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Neovim executable",
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilters,
        });

        if (files.Count > 0)
        {
            this.FindControl<TextBox>("NeovimPathBox")!.Text = files[0].Path.LocalPath;
        }
    }

    private async void Detect_Click(object? sender, RoutedEventArgs e)
    {
        var detected = EditorPathDetector.FindNeovimInPath();
        if (detected is null)
        {
            var msg = new MessageWindow("Neovim was not found in PATH.", "Detect Neovim");
            await msg.ShowDialog(this);
            return;
        }

        var pathBox = this.FindControl<TextBox>("NeovimPathBox")!;
        var currentPath = pathBox.Text ?? string.Empty;

        if (string.IsNullOrEmpty(currentPath))
        {
            pathBox.Text = detected;
        }
        else if (!string.Equals(currentPath, detected, StringComparison.OrdinalIgnoreCase))
        {
            var confirm = new ConfirmWindow(
                $"Detected Neovim at:\n{detected}\n\nReplace the current path?",
                "Detect Neovim");
            await confirm.ShowDialog(this);

            if (confirm.Confirmed)
            {
                pathBox.Text = detected;
            }
        }
    }

    private async void VimBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var fileTypeFilters = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[]
            {
                new FilePickerFileType("Executable Files") { Patterns = ["*.exe"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] },
            }
            : new[]
            {
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            };

        var files = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Vim executable",
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilters,
        });

        if (files.Count > 0)
        {
            this.FindControl<TextBox>("VimPathBox")!.Text = files[0].Path.LocalPath;
        }
    }

    private async void VimDetect_Click(object? sender, RoutedEventArgs e)
    {
        var detected = EditorPathDetector.FindVimInPath();
        if (detected is null)
        {
            var msg = new MessageWindow("Vim was not found in PATH.", "Detect Vim");
            await msg.ShowDialog(this);
            return;
        }

        var pathBox = this.FindControl<TextBox>("VimPathBox")!;
        var currentPath = pathBox.Text ?? string.Empty;

        if (string.IsNullOrEmpty(currentPath))
        {
            pathBox.Text = detected;
        }
        else if (!string.Equals(currentPath, detected, StringComparison.OrdinalIgnoreCase))
        {
            var confirm = new ConfirmWindow(
                $"Detected Vim at:\n{detected}\n\nReplace the current path?",
                "Detect Vim");
            await confirm.ShowDialog(this);

            if (confirm.Confirmed)
            {
                pathBox.Text = detected;
            }
        }
    }

    private async void FontAdd_Click(object? sender, RoutedEventArgs e)
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        var dialog = new FontPickerWindow();
        await dialog.ShowDialog(this);

        if (!string.IsNullOrWhiteSpace(dialog.SelectedFontName))
        {
            // Insert before the selected item, or at the end if nothing selected.
            int insertIndex = fontListBox.SelectedIndex >= 0
                ? fontListBox.SelectedIndex
                : fontListBox.Items.Count;
            fontListBox.Items.Insert(insertIndex, dialog.SelectedFontName);
            fontListBox.SelectedIndex = insertIndex;
            this.UpdateFontPriorityLive();
        }
    }

    private void FontRemove_Click(object? sender, RoutedEventArgs e)
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        if (fontListBox.SelectedIndex >= 0)
        {
            string? raw = GetRawFontEntry(fontListBox.SelectedItem);
            if (raw is not null && FontPriorityList.IsSentinel(raw))
            {
                return;
            }

            fontListBox.Items.RemoveAt(fontListBox.SelectedIndex);
            this.UpdateFontPriorityLive();
        }
    }

    private void FontMoveUp_Click(object? sender, RoutedEventArgs e)
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        int index = fontListBox.SelectedIndex;
        if (index > 0)
        {
            var item = fontListBox.Items[index]!;
            fontListBox.Items.RemoveAt(index);
            fontListBox.Items.Insert(index - 1, item);
            fontListBox.SelectedIndex = index - 1;
            this.UpdateFontPriorityLive();
        }
    }

    private void FontMoveDown_Click(object? sender, RoutedEventArgs e)
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        int index = fontListBox.SelectedIndex;
        if (index >= 0 && index < fontListBox.Items.Count - 1)
        {
            var item = fontListBox.Items[index]!;
            fontListBox.Items.RemoveAt(index);
            fontListBox.Items.Insert(index + 1, item);
            fontListBox.SelectedIndex = index + 1;
            this.UpdateFontPriorityLive();
        }
    }

    private void UpdateFontPriorityLive()
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        var fonts = new List<string>();
        foreach (var item in fontListBox.Items)
        {
            string? raw = GetRawFontEntry(item);
            if (raw is not null)
            {
                fonts.Add(raw);
            }
        }

        this.settings.FallbackFonts = fonts;
    }

    private void UpdateFontButtonStates()
    {
        var fontListBox = this.FindControl<ListBox>("FontListBox")!;
        int index = fontListBox.SelectedIndex;
        bool hasSelection = index >= 0;
        bool isSentinel = hasSelection && GetRawFontEntry(fontListBox.SelectedItem) is string raw && FontPriorityList.IsSentinel(raw);

        this.FindControl<Button>("FontRemoveButton")!.IsEnabled = hasSelection && !isSentinel;
        this.FindControl<Button>("FontMoveUpButton")!.IsEnabled = hasSelection && index > 0;
        this.FindControl<Button>("FontMoveDownButton")!.IsEnabled = hasSelection && index < fontListBox.Items.Count - 1;
    }

    /// <summary>
    /// Creates a display item for the font list. Sentinel entries are
    /// shown with a descriptive label and resolved font names; user
    /// font entries are plain strings.
    /// </summary>
    private object CreateFontListDisplayItem(string entry)
    {
        if (FontPriorityList.IsGuiFontSentinel(entry))
        {
            string resolved = string.Join(", ", this.GetCurrentGuiFontNames());
            string label = string.IsNullOrEmpty(resolved)
                ? "[Neovim guifont]"
                : $"[Neovim guifont]  ({resolved})";
            return new FontPriorityItem(FontPriorityList.GuiFontSentinel, label);
        }

        if (FontPriorityList.IsSystemMonoSentinel(entry))
        {
            string resolved = string.Join(", ", Helpers.GetDefaultFallbackFontNames());
            return new FontPriorityItem(FontPriorityList.SystemMonoSentinel, $"[System Monospace]  ({resolved})");
        }

        return entry;
    }

    private IReadOnlyList<string> GetCurrentGuiFontNames()
    {
        return this.currentGuiFontNames;
    }

    private void UpdatePathPanelVisibility()
    {
        var editorTypeCombo = this.FindControl<ComboBox>("EditorTypeCombo")!;
        bool isVim = editorTypeCombo.SelectedIndex == 1;
        this.FindControl<TextBlock>("NeovimPathLabel")!.IsVisible = !isVim;
        this.FindControl<TextBox>("NeovimPathBox")!.IsVisible = !isVim;
        this.FindControl<StackPanel>("NeovimPathPanel")!.IsVisible = !isVim;
        this.FindControl<TextBlock>("VimPathLabel")!.IsVisible = isVim;
        this.FindControl<TextBox>("VimPathBox")!.IsVisible = isVim;
        this.FindControl<StackPanel>("VimPathPanel")!.IsVisible = isVim;
    }

    private void PopulateFileAssociationList()
    {
        if (!ShellIntegrationService.IsSupported)
        {
            return;
        }

        var listBox = this.FindControl<ListBox>("FileAssocListBox")!;
        listBox.Items.Clear();

        var extensions = this.settings.FileAssociationExtensions.Count > 0
            ? this.settings.FileAssociationExtensions
            : ShellIntegrationService.DefaultExtensions.ToList();

        foreach (var ext in extensions.OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
        {
            listBox.Items.Add(FileAssocItem.Create(ext));
        }
    }

    private List<string> GetExtensionListFromUi()
    {
        var listBox = this.FindControl<ListBox>("FileAssocListBox")!;
        var result = new List<string>();
        foreach (var item in listBox.Items)
        {
            if (item is FileAssocItem row)
            {
                result.Add(row.Extension);
            }
        }

        return result;
    }

    private void RefreshShellIntegrationStatus()
    {
        if (!ShellIntegrationService.IsSupported)
        {
            return;
        }

        bool contextMenuRegistered = ShellIntegrationService.IsContextMenuRegistered();
        this.FindControl<TextBlock>("ContextMenuStatus")!.Text = contextMenuRegistered
            ? "Integrated — \"Open with AeroVim\" is in the Explorer right-click menu."
            : "Not integrated.";
        this.FindControl<Button>("ContextMenuButton")!.Content = contextMenuRegistered
            ? "Remove"
            : "Integrate";

        this.PopulateFileAssociationList();
    }

    private void ContextMenu_Click(object? sender, RoutedEventArgs e)
    {
        bool isRegistered = ShellIntegrationService.IsContextMenuRegistered();
        ShellIntegrationService.SetContextMenuRegistration(!isRegistered);
        this.RefreshShellIntegrationStatus();
    }

    private void FileAssocIntegrateAll_Click(object? sender, RoutedEventArgs e)
    {
        var extensions = this.GetExtensionListFromUi();
        ShellIntegrationService.RegisterAllExtensions(extensions);
        this.PopulateFileAssociationList();
    }

    private void FileAssocRemoveAll_Click(object? sender, RoutedEventArgs e)
    {
        var extensions = this.GetExtensionListFromUi();
        ShellIntegrationService.UnregisterAllExtensions(extensions);
        this.PopulateFileAssociationList();
    }

    private async void FileAssocAdd_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new InputWindow(
            "Enter file extensions separated by comma or semicolon:",
            "Add Extensions",
            "e.g. .txt, .log, .cfg");
        await dialog.ShowDialog(this);

        if (string.IsNullOrWhiteSpace(dialog.InputText))
        {
            return;
        }

        var listBox = this.FindControl<ListBox>("FileAssocListBox")!;
        var existing = this.GetExtensionListFromUi();

        var parts = dialog.InputText.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var ext = ShellIntegrationService.NormaliseExtension(part);
            if (ext is not null && !existing.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                existing.Add(ext);
                listBox.Items.Add(FileAssocItem.Create(ext));
            }
        }
    }

    private void FileAssocRemove_Click(object? sender, RoutedEventArgs e)
    {
        var listBox = this.FindControl<ListBox>("FileAssocListBox")!;

        // Collect selected items to remove (iterate in reverse to preserve indices).
        var toRemove = new List<int>();
        foreach (var item in listBox.Selection.SelectedItems)
        {
            int index = listBox.Items.IndexOf(item);
            if (index >= 0)
            {
                toRemove.Add(index);
            }
        }

        // Unregister from registry and remove from list.
        foreach (int index in toRemove.OrderByDescending(i => i))
        {
            if (listBox.Items[index] is FileAssocItem row)
            {
                ShellIntegrationService.SetExtensionRegistration(row.Extension, false);
            }

            listBox.Items.RemoveAt(index);
        }
    }

    private async void FileAssocClear_Click(object? sender, RoutedEventArgs e)
    {
        var confirm = new ConfirmWindow(
            "This will unregister all file associations and clear the list. Continue?",
            "Clear All Extensions");
        await confirm.ShowDialog(this);

        if (!confirm.Confirmed)
        {
            return;
        }

        var extensions = this.GetExtensionListFromUi();
        ShellIntegrationService.UnregisterAllExtensions(extensions);
        this.FindControl<ListBox>("FileAssocListBox")!.Items.Clear();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        switch (this.CloseReason)
        {
            case Result.Ok:
                this.SaveUiToSettings();
                this.settings.Save();
                break;
            case Result.Cancel:
                this.settings.Reload();
                break;
            case Result.NotClosed:
                this.CloseReason = Result.Cancel;
                this.settings.Reload();
                break;
        }
    }
}
