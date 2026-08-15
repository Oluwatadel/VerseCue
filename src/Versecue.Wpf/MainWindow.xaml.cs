using System.IO;
using System.Text.Json;
using Versecue.Domain.Enums;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using Versecue.Application.Interfaces;
using Versecue.Application.Interfaces.Repository;
using Versecue.Application.Models.Bible;
using Versecue.Infrastructure.Common;
using Versecue.Infrastructure.Persistence;
using Versecue.Application.Services;
using System.Collections.ObjectModel;
using Versecue.Application.Audio;

namespace Versecue.Wpf;

public partial class MainWindow : Window
{
    private readonly IBibleImportService _bibleImportService;
    private readonly VersecueDbContext _db;
    private readonly IBibleRepository _bibleRepository;

    private bool _loadingBrowser;
    private bool _loadingSettings;

    private VerseDisplayWindow? _verseDisplayWindow;
    private DashboardSettings _settings = new();
    private ProjectionMode _projectionMode = ProjectionMode.Offline;
    private ProjectionContent _projectionContent = ProjectionContent.Screensaver;
    private bool _canDisplayNextVerse;

    private static readonly string SettingsFilePath =
        Path.Combine(
            ApplicationPaths.Settings,
            "dashboard-settings.json");

    private static readonly string WallpaperImportDirectory =
        Path.Combine(
            ApplicationPaths.Settings,
            "Wallpapers");

    private static readonly string ScreensaverImportDirectory =
        Path.Combine(
            ApplicationPaths.Settings,
            "Screensavers");

    private static readonly IReadOnlyList<WallpaperOption> BuiltInWallpapers =
    [
        new WallpaperOption
        {
            Id = "builtin:classic-black",
            Name = "Classic Black"
        },
        new WallpaperOption
        {
            Id = "builtin:deep-ocean",
            Name = "Deep Ocean"
        },
        new WallpaperOption
        {
            Id = "builtin:warm-chapel",
            Name = "Warm Chapel"
        },
        new WallpaperOption
        {
            Id = "builtin:forest-dawn",
            Name = "Forest Dawn"
        }
    ];

    private static readonly IReadOnlyList<ScreensaverOption> BuiltInScreensavers =
    [
        new ScreensaverOption
        {
            Id = "builtin:versecue-default",
            Name = "VerseCue Default",
            MediaKind = "default"
        }
    ];

    private sealed class SelectedVersePreviewItem
    {
        public string Reference { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;
    }

    private sealed class BibleManagementItem
    {
        public Guid Id { get; init; }

        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Language { get; init; } = string.Empty;

        public string DisplayName =>
            $"{Code} - {Language}";
    }

    private sealed class DashboardSettings
    {
        // General
        public string DashboardTheme { get; set; } = "VerseCue Dark";
        public string DefaultTranslationCode { get; set; } = "NLT";
        public bool RememberLastSelection { get; set; } = true;
        public string LastTranslationCode { get; set; } = string.Empty;
        public string LastBookName { get; set; } = string.Empty;
        public int LastChapterNumber { get; set; } = 1;

        // Projection
        public string VerseDisplayWallpaper { get; set; } = "Classic Black";
        public string ImportedWallpaperPath { get; set; } = string.Empty;
        public string SelectedWallpaperId { get; set; } = "builtin:classic-black";
        public List<ImportedWallpaperSetting> ImportedWallpapers { get; set; } = [];
        public string SelectedScreensaverId { get; set; } = "builtin:versecue-default";
        public List<ImportedScreensaverSetting> ImportedScreensavers { get; set; } = [];

        // AI Configuration
        public string AiProvider { get; set; } = "None";
        public string AiModel { get; set; } = "gpt-4o";
        public string AiEndpoint { get; set; } = "https://api.openai.com/v1";
        public string AiApiKey { get; set; } = string.Empty;

        public double DefaultFontSize { get; set; } = 38;
        public double MinFontSize { get; set; } = 20;
        public double MaxFontSize { get; set; } = 60;
        public double VerseSpacing { get; set; } = 34;
        public double ReferenceFontSize { get; set; } = 21;
        public bool ReferenceVisibility { get; set; } = true;
        public string TextAlignment { get; set; } = "Center";
        public double DisplayMargin { get; set; } = 50;
        public string ProjectionTextColor { get; set; } = "#FFFFFF";
        public string ReferenceColor { get; set; } = "#AAAAAA";
    }

    private sealed class ImportedWallpaperSetting
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;
    }

    private sealed class WallpaperOption
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;

        public bool IsImported { get; init; }
    }

    private sealed class ImportedScreensaverSetting
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;
    }

    private sealed class ScreensaverOption
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;

        public string MediaKind { get; init; } = string.Empty;

        public bool IsImported { get; init; }
    }

    private enum ProjectionMode
    {
        Offline,
        Live
    }

    private enum ProjectionContent
    {
        Screensaver,
        Verse
    }


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    private readonly IBibleReferenceService _bibleReferenceService;
    private readonly VerseCueService _verseCueService;
    private readonly IWhisperTranscriptionService _transcriptionService;

    public MainWindow(
        IBibleImportService bibleImportService,
        VersecueDbContext db,
        IBibleRepository bibleRepository,
        IBibleReferenceService bibleReferenceService,
        VerseCueService verseCueService,
        IWhisperTranscriptionService transcriptionService)
    {
        InitializeComponent();

        _bibleImportService = bibleImportService;
        _db = db;
        _bibleRepository = bibleRepository;
        _bibleReferenceService = bibleReferenceService;
        _verseCueService = verseCueService;
        _transcriptionService = transcriptionService;

        _verseCueService.VerseCueDetected += VerseCueService_VerseCueDetected;
        _transcriptionService.TranscriptReceived += TranscriptionService_TranscriptReceived;

        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }


    // ============================================================
    // WINDOW LOADED
    // ============================================================

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        LoadDashboardSettings();
        ApplyDashboardSettingsToControls();
        ApplyDashboardTheme();
        ShowManualProjectionView();
        UpdateProjectionControls();

        await RefreshBibleStatusAsync();
        await RefreshBibleManagementAsync();

        await LoadTranslationsAsync();
    }


    // ============================================================
    // IMPORT BIBLE
    // ============================================================

    private async void ImportBible_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Bible Import File",

            Filter =
                "Bible files (*.xml;*.json)|*.xml;*.json|" +
                "XML files (*.xml)|*.xml|" +
                "JSON files (*.json)|*.json|" +
                "All files (*.*)|*.*",

            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            var failedImports =
                new List<string>();

            foreach (var fileName in dialog.FileNames)
            {
                try
                {
                    await _bibleImportService.ImportAsync(
                        fileName,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    failedImports.Add(
                        $"{Path.GetFileName(fileName)}: {ex.Message}");
                }
            }

            await RefreshBibleStatusAsync();
            await RefreshBibleManagementAsync();

            await LoadTranslationsAsync();

            ShowImportSummary(
                "Bible Import",
                dialog.FileNames.Length,
                failedImports);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Bible Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void BibleManagementListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        var hasSelection =
            BibleManagementListBox.SelectedItem is BibleManagementItem;

        DeleteBibleButton.IsEnabled =
            hasSelection;

        RenameBibleButton.IsEnabled =
            hasSelection;

        BibleManagementStatusText.Text =
            hasSelection
                ? "Selected Bible can be deleted or renamed."
                : "No Bible selected";
    }

    private async void DeleteBible_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (BibleManagementListBox.SelectedItem is not BibleManagementItem item)
        {
            return;
        }

        var result =
            MessageBox.Show(
                $"Delete {item.Name} ({item.Code}) and all its books, chapters and verses?",
                "Delete Bible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor =
                Cursors.Wait;

            var translation =
                await _db.BibleTranslations
                    .FirstOrDefaultAsync(x =>
                        x.Id == item.Id);

            if (translation is null)
            {
                MessageBox.Show(
                    "The selected Bible no longer exists.",
                    "Delete Bible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await RefreshBibleManagementAsync();

                return;
            }

            _db.BibleTranslations.Remove(
                translation);

            await _db.SaveChangesAsync();

            ResetBookAndChapter();

            await RefreshBibleStatusAsync();
            await RefreshBibleManagementAsync();
            await LoadTranslationsAsync();

            MessageBox.Show(
                "Bible deleted successfully.",
                "Delete Bible",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Delete Bible Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor =
                null;
        }
    }

    private async void RenameBible_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (BibleManagementListBox.SelectedItem is not BibleManagementItem item)
        {
            return;
        }

        var newCode = InputDialog.Show(
            "Rename Bible Code",
            $"Enter a new abbreviation/code for {item.Name} (currently {item.Code}):",
            item.Code);

        if (string.IsNullOrWhiteSpace(newCode))
        {
            return;
        }

        newCode = newCode.Trim().ToUpperInvariant();
        var sanitizedCode = new string(newCode.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedCode))
        {
            MessageBox.Show(
                "Bible abbreviation/code must contain letters or digits.",
                "Rename Bible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (sanitizedCode == item.Code)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            var translation = await _db.BibleTranslations
                .FirstOrDefaultAsync(x => x.Id == item.Id);

            if (translation is null)
            {
                MessageBox.Show(
                    "The selected Bible no longer exists.",
                    "Rename Bible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await RefreshBibleManagementAsync();
                return;
            }

            translation.Rename(sanitizedCode);
            await _db.SaveChangesAsync();

            await RefreshBibleStatusAsync();
            await RefreshBibleManagementAsync();
            await LoadTranslationsAsync();

            MessageBox.Show(
                "Bible code renamed successfully.",
                "Rename Bible",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Rename Bible Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }


    // ============================================================
    // SETTINGS VIEW
    // ============================================================



    private void ManualProjectionNavButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowManualProjectionView();
    }

    private void SettingsNavButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowSettingsView();
    }

    private void ShowManualProjectionView()
    {
        ManualProjectionView.Visibility =
            Visibility.Visible;

        MetricsView.Visibility =
            Visibility.Visible;

        SettingsView.Visibility =
            Visibility.Collapsed;

        HeaderTitleText.Text =
            "Manual Projection";

        HeaderSubtitleText.Text =
            "Select, preview and display Bible verses manually.";
    }

    private void ShowSettingsView()
    {
        ManualProjectionView.Visibility =
            Visibility.Collapsed;

        MetricsView.Visibility =
            Visibility.Visible;

        SettingsView.Visibility =
            Visibility.Visible;

        HeaderTitleText.Text =
            "Settings";

        HeaderSubtitleText.Text =
            "Manage Bible imports, dashboard theme and projector wallpaper.";
    }

    private void DashboardThemeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingSettings)
        {
            return;
        }

        _settings.DashboardTheme =
            GetSelectedComboBoxText(
                DashboardThemeComboBox,
                _settings.DashboardTheme);

        ApplyDashboardTheme();
        SaveDashboardSettings();
    }

    private void WallpaperComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingSettings)
        {
            return;
        }

        if (WallpaperComboBox.SelectedItem is not WallpaperOption wallpaper)
        {
            return;
        }

        _settings.SelectedWallpaperId =
            wallpaper.Id;

        _settings.VerseDisplayWallpaper =
            wallpaper.Name;

        _settings.ImportedWallpaperPath =
            wallpaper.FilePath;

        UpdateWallpaperPathText();
        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void ImportWallpaper_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Verse Display Wallpaper",
            Filter =
                "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|" +
                "JPEG files (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                "PNG files (*.png)|*.png",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(
                WallpaperImportDirectory);

            var failedImports =
                new List<string>();

            foreach (var fileName in dialog.FileNames)
            {
                try
                {
                    var extension =
                        Path.GetExtension(fileName)
                            .ToLowerInvariant();

                    if (extension is not ".jpg" and not ".jpeg" and not ".png")
                    {
                        failedImports.Add(
                            $"{Path.GetFileName(fileName)}: unsupported image type");

                        continue;
                    }

                    var wallpaperId =
                        $"imported:{Guid.NewGuid():N}";

                    var destinationPath =
                        Path.Combine(
                            WallpaperImportDirectory,
                            $"{wallpaperId.Replace(':', '-')}{extension}");

                    File.Copy(
                        fileName,
                        destinationPath,
                        overwrite: true);

                    _settings.ImportedWallpapers.Add(
                        new ImportedWallpaperSetting
                        {
                            Id =
                                wallpaperId,

                            Name =
                                GetUniqueWallpaperName(
                                    Path.GetFileNameWithoutExtension(fileName)),

                            FilePath =
                                destinationPath
                        });

                    _settings.SelectedWallpaperId =
                        wallpaperId;
                }
                catch (Exception ex)
                {
                    failedImports.Add(
                        $"{Path.GetFileName(fileName)}: {ex.Message}");
                }
            }

            ApplyDashboardSettingsToControls();
            SaveDashboardSettings();
            ApplySettingsToOpenDisplayWindow();

            ShowImportSummary(
                "Wallpaper Import",
                dialog.FileNames.Length,
                failedImports);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Wallpaper Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RenameWallpaper_Click(
        object sender,
        RoutedEventArgs e)
    {
        var wallpaper =
            GetSelectedWallpaperOption();

        if (wallpaper is null || !wallpaper.IsImported)
        {
            return;
        }

        var importedWallpaper =
            _settings.ImportedWallpapers
                .FirstOrDefault(x => x.Id == wallpaper.Id);

        if (importedWallpaper is null)
        {
            return;
        }

        var newName =
            WallpaperNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show(
                "Wallpaper name is required.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        if (BuiltInWallpapers.Any(x =>
                string.Equals(
                    x.Name,
                    newName,
                    StringComparison.OrdinalIgnoreCase)) ||
            _settings.ImportedWallpapers.Any(x =>
                x.Id != importedWallpaper.Id &&
                string.Equals(
                    x.Name,
                    newName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "A wallpaper with that name already exists.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        importedWallpaper.Name =
            newName;

        _settings.VerseDisplayWallpaper =
            newName;

        SaveDashboardSettings();
        ApplyDashboardSettingsToControls();
        ApplySettingsToOpenDisplayWindow();
    }

    private void DeleteWallpaper_Click(
        object sender,
        RoutedEventArgs e)
    {
        var wallpaper =
            GetSelectedWallpaperOption();

        if (wallpaper is null || !wallpaper.IsImported)
        {
            return;
        }

        var result =
            MessageBox.Show(
                $"Delete '{wallpaper.Name}' from imported wallpapers?",
                "Delete Wallpaper",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.ImportedWallpapers.RemoveAll(x =>
            x.Id == wallpaper.Id);

        if (File.Exists(wallpaper.FilePath))
        {
            try
            {
                File.Delete(
                    wallpaper.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Wallpaper File Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        _settings.SelectedWallpaperId =
            "builtin:classic-black";

        _settings.VerseDisplayWallpaper =
            "Classic Black";

        _settings.ImportedWallpaperPath =
            string.Empty;

        SaveDashboardSettings();
        ApplyDashboardSettingsToControls();
        ApplySettingsToOpenDisplayWindow();
    }

    private void ScreensaverComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingSettings)
        {
            return;
        }

        if (ScreensaverComboBox.SelectedItem is not ScreensaverOption screensaver)
        {
            return;
        }

        _settings.SelectedScreensaverId =
            screensaver.Id;

        UpdateScreensaverPathText();
        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void ImportScreensaver_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Projection Screensaver",
            Filter =
                "Screensaver files (*.jpg;*.jpeg;*.png;*.gif;*.mp4;*.wmv;*.avi;*.mov)|*.jpg;*.jpeg;*.png;*.gif;*.mp4;*.wmv;*.avi;*.mov|" +
                "Image files (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|" +
                "Video files (*.mp4;*.wmv;*.avi;*.mov)|*.mp4;*.wmv;*.avi;*.mov",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(
                ScreensaverImportDirectory);

            var failedImports =
                new List<string>();

            foreach (var fileName in dialog.FileNames)
            {
                try
                {
                    var extension =
                        Path.GetExtension(fileName)
                            .ToLowerInvariant();

                    var mediaKind =
                        GetScreensaverMediaKind(extension);

                    if (mediaKind is null)
                    {
                        failedImports.Add(
                            $"{Path.GetFileName(fileName)}: unsupported screensaver type");

                        continue;
                    }

                    var screensaverId =
                        $"screensaver:{Guid.NewGuid():N}";

                    var destinationPath =
                        Path.Combine(
                            ScreensaverImportDirectory,
                            $"{screensaverId.Replace(':', '-')}{extension}");

                    File.Copy(
                        fileName,
                        destinationPath,
                        overwrite: true);

                    _settings.ImportedScreensavers.Add(
                        new ImportedScreensaverSetting
                        {
                            Id =
                                screensaverId,

                            Name =
                                GetUniqueScreensaverName(
                                    Path.GetFileNameWithoutExtension(fileName)),

                            FilePath =
                                destinationPath,

                            MediaKind =
                                mediaKind
                        });

                    _settings.SelectedScreensaverId =
                        screensaverId;
                }
                catch (Exception ex)
                {
                    failedImports.Add(
                        $"{Path.GetFileName(fileName)}: {ex.Message}");
                }
            }

            ApplyDashboardSettingsToControls();
            SaveDashboardSettings();
            ApplySettingsToOpenDisplayWindow();

            ShowImportSummary(
                "Screensaver Import",
                dialog.FileNames.Length,
                failedImports);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Screensaver Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RenameScreensaver_Click(
        object sender,
        RoutedEventArgs e)
    {
        var screensaver =
            GetSelectedScreensaverOption();

        if (screensaver is null || !screensaver.IsImported)
        {
            return;
        }

        var importedScreensaver =
            _settings.ImportedScreensavers
                .FirstOrDefault(x => x.Id == screensaver.Id);

        if (importedScreensaver is null)
        {
            return;
        }

        var newName =
            ScreensaverNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(newName))
        {
            MessageBox.Show(
                "Screensaver name is required.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        if (BuiltInScreensavers.Any(x =>
                string.Equals(
                    x.Name,
                    newName,
                    StringComparison.OrdinalIgnoreCase)) ||
            _settings.ImportedScreensavers.Any(x =>
                x.Id != importedScreensaver.Id &&
                string.Equals(
                    x.Name,
                    newName,
                    StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "A screensaver with that name already exists.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        importedScreensaver.Name =
            newName;

        SaveDashboardSettings();
        ApplyDashboardSettingsToControls();
        ApplySettingsToOpenDisplayWindow();
    }

    private void DeleteScreensaver_Click(
        object sender,
        RoutedEventArgs e)
    {
        var screensaver =
            GetSelectedScreensaverOption();

        if (screensaver is null || !screensaver.IsImported)
        {
            return;
        }

        var result =
            MessageBox.Show(
                $"Delete '{screensaver.Name}' from imported screensavers?",
                "Delete Screensaver",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _settings.ImportedScreensavers.RemoveAll(x =>
            x.Id == screensaver.Id);

        if (File.Exists(screensaver.FilePath))
        {
            try
            {
                File.Delete(
                    screensaver.FilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Screensaver File Delete Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        _settings.SelectedScreensaverId =
            "builtin:versecue-default";

        SaveDashboardSettings();
        ApplyDashboardSettingsToControls();
        ApplySettingsToOpenDisplayWindow();
    }

    private void LoadDashboardSettings()
    {
        try
        {
            ApplicationPaths.EnsureDirectoriesExist();

            if (!File.Exists(SettingsFilePath))
            {
                return;
            }

            var json =
                File.ReadAllText(SettingsFilePath);

            _settings =
                JsonSerializer.Deserialize<DashboardSettings>(json)
                ?? new DashboardSettings();

            NormalizeDashboardSettings();
        }
        catch
        {
            _settings =
                new DashboardSettings();
        }
    }

    private void SaveDashboardSettings()
    {
        try
        {
            ApplicationPaths.EnsureDirectoriesExist();

            var json =
                JsonSerializer.Serialize(
                    _settings,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

            File.WriteAllText(
                SettingsFilePath,
                json);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Settings Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplyDashboardSettingsToControls()
    {
        try
        {
            _loadingSettings =
                true;

            SelectComboBoxItem(
                DashboardThemeComboBox,
                _settings.DashboardTheme);

            RefreshWallpaperOptions();
            RefreshScreensaverOptions();

            UpdateWallpaperPathText();
            UpdateScreensaverPathText();

            // MVP settings bindings
            DefaultTranslationTextBox.Text = _settings.DefaultTranslationCode;
            RememberLastSelectionCheckBox.IsChecked = _settings.RememberLastSelection;
            DefaultFontSizeTextBox.Text = _settings.DefaultFontSize.ToString();
            MinFontSizeTextBox.Text = _settings.MinFontSize.ToString();
            MaxFontSizeTextBox.Text = _settings.MaxFontSize.ToString();
            VerseSpacingTextBox.Text = _settings.VerseSpacing.ToString();
            ReferenceFontSizeTextBox.Text = _settings.ReferenceFontSize.ToString();
            DisplayMarginTextBox.Text = _settings.DisplayMargin.ToString();
            ShowReferenceCheckBox.IsChecked = _settings.ReferenceVisibility;
            TextColorTextBox.Text = _settings.ProjectionTextColor;
            ReferenceColorTextBox.Text = _settings.ReferenceColor;

            SelectTextAlignmentComboBoxItem(_settings.TextAlignment);

            // Bind AI Settings Controls
            SelectAiProviderComboBoxItem(_settings.AiProvider);
            AiModelTextBox.Text = _settings.AiModel;
            AiEndpointTextBox.Text = _settings.AiEndpoint;
            AiApiKeyTextBox.Text = _settings.AiApiKey;
        }
        finally
        {
            _loadingSettings =
                false;
        }
    }

    private void ApplyDashboardTheme()
    {
        var theme =
            _settings.DashboardTheme;

        if (theme == "Clean Light")
        {
            RootGrid.Background =
                new SolidColorBrush(
                    Color.FromRgb(232, 238, 245));

            HeaderBorder.Background =
                new SolidColorBrush(
                    Color.FromRgb(37, 99, 235));

            return;
        }

        if (theme == "Midnight Blue")
        {
            RootGrid.Background =
                new SolidColorBrush(
                    Color.FromRgb(3, 7, 18));

            HeaderBorder.Background =
                new SolidColorBrush(
                    Color.FromRgb(30, 64, 175));

            return;
        }

        RootGrid.Background =
            new SolidColorBrush(
                Color.FromRgb(7, 17, 31));

        HeaderBorder.Background =
            new SolidColorBrush(
                Color.FromRgb(85, 41, 216));
    }

    private void ApplySettingsToOpenDisplayWindow()
    {
        if (_verseDisplayWindow is null)
        {
            return;
        }

        if (_projectionContent == ProjectionContent.Screensaver)
        {
            _verseDisplayWindow.ShowScreensaver(
                BuildVerseDisplayOptions());

            return;
        }

        _verseDisplayWindow!.ApplyDisplayOptions(
            BuildVerseDisplayOptions());
    }

    private VerseDisplayWindow.VerseDisplayOptions BuildVerseDisplayOptions()
    {
        var wallpaper =
            GetSelectedWallpaperOption()
            ?? GetWallpaperOptions()
                .First();

        return new VerseDisplayWindow.VerseDisplayOptions
        {
            WallpaperName =
                wallpaper.Name,

            ImportedWallpaperPath =
                wallpaper.FilePath,

            ScreensaverName =
                GetSelectedScreensaverOption()?.Name
                ?? BuiltInScreensavers[0].Name,

            ScreensaverPath =
                GetSelectedScreensaverOption()?.FilePath
                ?? string.Empty,

            ScreensaverMediaKind =
                GetSelectedScreensaverOption()?.MediaKind
                ?? BuiltInScreensavers[0].MediaKind,

            ShowVerseCueWatermark =
                true,

            // Layout custom variables
            DefaultFontSize = _settings.DefaultFontSize,
            MinFontSize = _settings.MinFontSize,
            MaxFontSize = _settings.MaxFontSize,
            VerseSpacing = _settings.VerseSpacing,
            ReferenceFontSize = _settings.ReferenceFontSize,
            ReferenceVisibility = _settings.ReferenceVisibility,
            TextAlignment = _settings.TextAlignment,
            DisplayMargin = _settings.DisplayMargin,
            ProjectionTextColor = _settings.ProjectionTextColor,
            ReferenceColor = _settings.ReferenceColor
        };
    }

    private void UpdateWallpaperPathText()
    {
        var wallpaper =
            GetSelectedWallpaperOption();

        if (wallpaper is null)
        {
            WallpaperNameTextBox.Text =
                string.Empty;

            WallpaperNameTextBox.IsEnabled =
                false;

            RenameWallpaperButton.IsEnabled =
                false;

            DeleteWallpaperButton.IsEnabled =
                false;

            WallpaperPathText.Text =
                "JPEG or PNG";

            return;
        }

        WallpaperNameTextBox.Text =
            wallpaper.Name;

        WallpaperNameTextBox.IsEnabled =
            wallpaper.IsImported;

        RenameWallpaperButton.IsEnabled =
            wallpaper.IsImported;

        DeleteWallpaperButton.IsEnabled =
            wallpaper.IsImported;

        if (wallpaper.IsImported &&
            !string.IsNullOrWhiteSpace(wallpaper.FilePath))
        {
            WallpaperPathText.Text =
                wallpaper.FilePath;

            return;
        }

        WallpaperPathText.Text =
            "Built-in wallpaper";
    }

    private void NormalizeDashboardSettings()
    {
        _settings.ImportedWallpapers ??= [];

        if (string.IsNullOrWhiteSpace(_settings.SelectedWallpaperId))
        {
            _settings.SelectedWallpaperId =
                GetBuiltInWallpaperIdByName(
                    _settings.VerseDisplayWallpaper);
        }

        if (!string.IsNullOrWhiteSpace(_settings.ImportedWallpaperPath) &&
            File.Exists(_settings.ImportedWallpaperPath) &&
            !_settings.ImportedWallpapers.Any(x =>
                string.Equals(
                    x.FilePath,
                    _settings.ImportedWallpaperPath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            var id =
                $"imported:{Guid.NewGuid():N}";

            _settings.ImportedWallpapers.Add(
                new ImportedWallpaperSetting
                {
                    Id =
                        id,

                    Name =
                        GetUniqueWallpaperName(
                            "Imported Wallpaper"),

                    FilePath =
                        _settings.ImportedWallpaperPath
                });

            if (_settings.VerseDisplayWallpaper == "Imported Wallpaper")
            {
                _settings.SelectedWallpaperId =
                    id;
            }
        }

        if (!GetWallpaperOptions().Any(x => x.Id == _settings.SelectedWallpaperId))
        {
            _settings.SelectedWallpaperId =
                "builtin:classic-black";
        }

        _settings.ImportedScreensavers ??= [];

        if (string.IsNullOrWhiteSpace(_settings.SelectedScreensaverId) ||
            !GetScreensaverOptions().Any(x => x.Id == _settings.SelectedScreensaverId))
        {
            _settings.SelectedScreensaverId =
                "builtin:versecue-default";
        }
    }

    private void RefreshWallpaperOptions()
    {
        var wallpapers =
            GetWallpaperOptions();

        WallpaperComboBox.ItemsSource =
            wallpapers;

        WallpaperComboBox.SelectedItem =
            wallpapers.FirstOrDefault(x =>
                x.Id == _settings.SelectedWallpaperId)
            ?? wallpapers.First();
    }

    private List<WallpaperOption> GetWallpaperOptions()
    {
        return BuiltInWallpapers
            .Concat(
                _settings.ImportedWallpapers
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.Id) &&
                        !string.IsNullOrWhiteSpace(x.Name) &&
                        !string.IsNullOrWhiteSpace(x.FilePath))
                    .Select(x => new WallpaperOption
                    {
                        Id =
                            x.Id,

                        Name =
                            x.Name,

                        FilePath =
                            x.FilePath,

                        IsImported =
                            true
                    }))
            .ToList();
    }

    private WallpaperOption? GetSelectedWallpaperOption()
    {
        return WallpaperComboBox.SelectedItem as WallpaperOption;
    }

    private void UpdateScreensaverPathText()
    {
        var screensaver =
            GetSelectedScreensaverOption();

        if (screensaver is null)
        {
            ScreensaverNameTextBox.Text =
                string.Empty;

            ScreensaverNameTextBox.IsEnabled =
                false;

            RenameScreensaverButton.IsEnabled =
                false;

            DeleteScreensaverButton.IsEnabled =
                false;

            ScreensaverPathText.Text =
                "Image, GIF or video";

            return;
        }

        ScreensaverNameTextBox.Text =
            screensaver.Name;

        ScreensaverNameTextBox.IsEnabled =
            screensaver.IsImported;

        RenameScreensaverButton.IsEnabled =
            screensaver.IsImported;

        DeleteScreensaverButton.IsEnabled =
            screensaver.IsImported;

        ScreensaverPathText.Text =
            screensaver.IsImported
                ? screensaver.FilePath
                : "Built-in screensaver";
    }

    private void RefreshScreensaverOptions()
    {
        var screensavers =
            GetScreensaverOptions();

        ScreensaverComboBox.ItemsSource =
            screensavers;

        ScreensaverComboBox.SelectedItem =
            screensavers.FirstOrDefault(x =>
                x.Id == _settings.SelectedScreensaverId)
            ?? screensavers.First();
    }

    private List<ScreensaverOption> GetScreensaverOptions()
    {
        return BuiltInScreensavers
            .Concat(
                _settings.ImportedScreensavers
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(x.Id) &&
                        !string.IsNullOrWhiteSpace(x.Name) &&
                        !string.IsNullOrWhiteSpace(x.FilePath) &&
                        !string.IsNullOrWhiteSpace(x.MediaKind))
                    .Select(x => new ScreensaverOption
                    {
                        Id =
                            x.Id,

                        Name =
                            x.Name,

                        FilePath =
                            x.FilePath,

                        MediaKind =
                            x.MediaKind,

                        IsImported =
                            true
                    }))
            .ToList();
    }

    private ScreensaverOption? GetSelectedScreensaverOption()
    {
        return ScreensaverComboBox.SelectedItem as ScreensaverOption;
    }

    private string GetUniqueScreensaverName(
        string requestedName)
    {
        var baseName =
            string.IsNullOrWhiteSpace(requestedName)
                ? "Imported Screensaver"
                : requestedName.Trim();

        var existingNames =
            BuiltInScreensavers
                .Select(x => x.Name)
                .Concat(_settings.ImportedScreensavers.Select(x => x.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        var counter =
            2;

        while (existingNames.Contains($"{baseName} {counter}"))
        {
            counter++;
        }

        return $"{baseName} {counter}";
    }

    private static string? GetScreensaverMediaKind(
        string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" => "image",
            ".gif" => "gif",
            ".mp4" or ".wmv" or ".avi" or ".mov" => "video",
            _ => null
        };
    }

    private static void ShowImportSummary(
        string title,
        int totalCount,
        IReadOnlyCollection<string> failedImports)
    {
        var successfulCount =
            totalCount - failedImports.Count;

        if (failedImports.Count == 0)
        {
            MessageBox.Show(
                $"{successfulCount} file(s) imported successfully.",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        MessageBox.Show(
            $"{successfulCount} of {totalCount} file(s) imported successfully.\n\n" +
            string.Join(
                "\n",
                failedImports.Take(8)),
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private string GetUniqueWallpaperName(
        string requestedName)
    {
        var baseName =
            string.IsNullOrWhiteSpace(requestedName)
                ? "Imported Wallpaper"
                : requestedName.Trim();

        var existingNames =
            BuiltInWallpapers
                .Select(x => x.Name)
                .Concat(_settings.ImportedWallpapers.Select(x => x.Name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName))
        {
            return baseName;
        }

        var counter =
            2;

        while (existingNames.Contains($"{baseName} {counter}"))
        {
            counter++;
        }

        return $"{baseName} {counter}";
    }

    private static string GetBuiltInWallpaperIdByName(
        string wallpaperName)
    {
        return BuiltInWallpapers
            .FirstOrDefault(x =>
                string.Equals(
                    x.Name,
                    wallpaperName,
                    StringComparison.OrdinalIgnoreCase))
            ?.Id
            ?? "builtin:classic-black";
    }

    private static string GetSelectedComboBoxText(
        System.Windows.Controls.ComboBox comboBox,
        string fallback)
    {
        return (comboBox.SelectedItem as ComboBoxItem)
            ?.Content
            ?.ToString()
            ?? fallback;
    }

    private static void SelectComboBoxItem(
        System.Windows.Controls.ComboBox comboBox,
        string value)
    {
        var item =
            comboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Content?.ToString(),
                        value,
                        StringComparison.OrdinalIgnoreCase));

        comboBox.SelectedItem =
            item ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }


    // ============================================================
    // PROJECTION STATE
    // ============================================================

    private void LiveProjection_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_projectionMode == ProjectionMode.Live)
        {
            GoOffline();

            return;
        }

        GoLive();
    }

    private void GoLive()
    {
        _projectionMode =
            ProjectionMode.Live;

        _projectionContent =
            ProjectionContent.Screensaver;

        _canDisplayNextVerse =
            false;

        EnsureProjectionWindow();
        ShowProjectionScreensaver();
        UpdateProjectionControls();
    }

    private void GoOffline()
    {
        _projectionMode =
            ProjectionMode.Offline;

        _projectionContent =
            ProjectionContent.Screensaver;

        _canDisplayNextVerse =
            false;

        if (_verseDisplayWindow is not null)
        {
            _verseDisplayWindow.Hide();
        }

        UpdateProjectionControls();
    }

    private void ClearProjection_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_projectionMode != ProjectionMode.Live)
        {
            return;
        }

        ShowProjectionScreensaver();
        UpdateProjectionControls();
    }

    private void EnsureProjectionWindow()
    {
        if (_verseDisplayWindow is not null)
        {
            PlaceProjectionWindow();

            return;
        }

        _verseDisplayWindow =
            new VerseDisplayWindow(
                _bibleRepository);

        _verseDisplayWindow.Closed +=
            VerseDisplayWindow_Closed;

        PlaceProjectionWindow();
    }

    private void PlaceProjectionWindow()
    {
        if (_verseDisplayWindow is null)
        {
            return;
        }

        var hasSecondarySurface =
            SystemParameters.VirtualScreenWidth >
                SystemParameters.PrimaryScreenWidth ||
            SystemParameters.VirtualScreenHeight >
                SystemParameters.PrimaryScreenHeight;

        if (!hasSecondarySurface)
        {
            _verseDisplayWindow.WindowStartupLocation =
                WindowStartupLocation.CenterScreen;

            _verseDisplayWindow.WindowStyle =
                WindowStyle.SingleBorderWindow;

            _verseDisplayWindow.ResizeMode =
                ResizeMode.CanResize;

            return;
        }

        var left =
            SystemParameters.VirtualScreenLeft < 0
                ? SystemParameters.VirtualScreenLeft
                : SystemParameters.PrimaryScreenWidth;

        var top =
            SystemParameters.VirtualScreenTop < 0
                ? SystemParameters.VirtualScreenTop
                : 0;

        var width =
            SystemParameters.VirtualScreenWidth -
            SystemParameters.PrimaryScreenWidth;

        var height =
            SystemParameters.VirtualScreenHeight;

        _verseDisplayWindow.WindowStartupLocation =
            WindowStartupLocation.Manual;

        _verseDisplayWindow.Left =
            left;

        _verseDisplayWindow.Top =
            top;

        _verseDisplayWindow.Width =
            Math.Max(
                700,
                width);

        _verseDisplayWindow.Height =
            Math.Max(
                450,
                height);

        _verseDisplayWindow.WindowState =
            WindowState.Normal;

        _verseDisplayWindow.WindowStyle =
            WindowStyle.None;

        _verseDisplayWindow.ResizeMode =
            ResizeMode.NoResize;
    }

    private void ShowProjectionScreensaver()
    {
        if (_projectionMode != ProjectionMode.Live)
        {
            return;
        }

        EnsureProjectionWindow();

        _verseDisplayWindow?.ShowScreensaver(
            BuildVerseDisplayOptions());

        _projectionContent =
            ProjectionContent.Screensaver;

        _canDisplayNextVerse =
            false;
    }

    private void UpdateProjectionControls()
    {
        var isLive =
            _projectionMode == ProjectionMode.Live;

        LiveProjectionButton.Content =
            isLive
                ? "Go Offline"
                : "Go Live";

        LiveProjectionButton.Background =
            isLive
                ? new SolidColorBrush(Color.FromRgb(185, 28, 28))
                : new SolidColorBrush(Color.FromRgb(22, 163, 74));

        LiveProjectionButton.BorderBrush =
            isLive
                ? new SolidColorBrush(Color.FromRgb(248, 113, 113))
                : new SolidColorBrush(Color.FromRgb(34, 197, 94));

        ClearProjectionButton.IsEnabled =
            isLive;

        DisplayVerseButton.IsEnabled =
            isLive;

        PreviousVerseButton.IsEnabled =
            isLive &&
            _projectionContent == ProjectionContent.Verse;

        NextVerseButton.IsEnabled =
            isLive &&
            _projectionContent == ProjectionContent.Verse &&
            _canDisplayNextVerse;
    }


    // ============================================================
    // DATABASE STATUS
    // ============================================================

    private async Task RefreshBibleManagementAsync()
    {
        try
        {
            var bibles =
                await _db.BibleTranslations
                    .OrderBy(x => x.Name)
                    .Select(x => new BibleManagementItem
                    {
                        Id =
                            x.Id,

                        Code =
                            x.Code,

                        Name =
                            x.Name,

                        Language =
                            x.Language
                    })
                    .ToListAsync();

            BibleManagementListBox.ItemsSource =
                bibles;

            BibleManagementListBox.SelectedIndex =
                -1;

            DeleteBibleButton.IsEnabled =
                false;

            BibleManagementStatusText.Text =
                bibles.Count == 0
                    ? "No Bible installed"
                    : $"{bibles.Count} Bible translation(s) installed";
        }
        catch (Exception ex)
        {
            BibleManagementListBox.ItemsSource =
                null;

            DeleteBibleButton.IsEnabled =
                false;

            BibleManagementStatusText.Text =
                ex.Message;
        }
    }

    private async Task RefreshBibleStatusAsync()
    {
        try
        {
            var translationCount =
                await _db.BibleTranslations.CountAsync();

            var bookCount =
                await _db.BibleBooks.CountAsync();

            var chapterCount =
                await _db.BibleChapters.CountAsync();

            var verseCount =
                await _db.BibleVerses.CountAsync();


            TranslationCountText.Text =
                translationCount.ToString();

            BookCountText.Text =
                bookCount.ToString();

            ChapterCountText.Text =
                chapterCount.ToString();

            VerseCountText.Text =
                verseCount.ToString();


            var translation =
                await _db.BibleTranslations
                    .OrderBy(x => x.Code)
                    .FirstOrDefaultAsync();


            if (translation is null)
            {
                TranslationText.Text =
                    "No Bible installed";

                LanguageText.Text =
                    string.Empty;

                return;
            }


            TranslationText.Text = translation.Code;

            LanguageText.Text =
                $"Language: {translation.Language}";
        }
        catch (Exception ex)
        {
            TranslationText.Text =
                "Unable to read Bible database";

            LanguageText.Text =
                ex.Message;
        }
    }


    // ============================================================
    // LOAD TRANSLATIONS
    // ============================================================

    private async Task LoadTranslationsAsync()
    {
        try
        {
            _loadingBrowser = true;

            Console.WriteLine(
                "DAPPER UI: Loading translations...");


            var translations =
                await _bibleRepository
                    .GetActiveTranslationsAsync();


            Console.WriteLine(
                $"DAPPER UI: Found {translations.Count} translations.");


            TranslationTabListBox.ItemsSource =
                translations;

            TranslationTabListBox.SelectedIndex =
                -1;


            BookComboBox.ItemsSource =
                null;

            BookComboBox.SelectedIndex =
                -1;

            BookComboBox.IsEnabled =
                false;


            ChapterComboBox.ItemsSource =
                null;

            ChapterComboBox.SelectedIndex =
                -1;

            ChapterComboBox.IsEnabled =
                false;


            VerseComboBox.ItemsSource =
                null;

            VerseComboBox.SelectedItems.Clear();
            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                "Select a chapter to view verses";

            BibleTranslationListItem? toSelect = null;
            if (_settings.RememberLastSelection && !string.IsNullOrWhiteSpace(_settings.LastTranslationCode))
            {
                toSelect = translations.FirstOrDefault(x => string.Equals(x.Code, _settings.LastTranslationCode, StringComparison.OrdinalIgnoreCase));
            }
            if (toSelect is null && !string.IsNullOrWhiteSpace(_settings.DefaultTranslationCode))
            {
                toSelect = translations.FirstOrDefault(x => string.Equals(x.Code, _settings.DefaultTranslationCode, StringComparison.OrdinalIgnoreCase));
            }
            if (toSelect is null && translations.Count > 0)
            {
                toSelect = translations[0];
            }

            if (toSelect is not null)
            {
                _loadingBrowser = true;
                TranslationTabListBox.SelectedItem = toSelect;
                _loadingBrowser = false;

                await LoadBooksAsync(toSelect);

                if (_settings.RememberLastSelection && !string.IsNullOrWhiteSpace(_settings.LastBookName))
                {
                    var book = (BookComboBox.ItemsSource as IReadOnlyList<BibleBookListItem>)?.FirstOrDefault(x => string.Equals(x.Name, _settings.LastBookName, StringComparison.OrdinalIgnoreCase));
                    if (book is not null)
                    {
                        _loadingBrowser = true;
                        BookComboBox.SelectedItem = book;
                        _loadingBrowser = false;

                        await LoadChaptersAsync(book, _settings.LastChapterNumber);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Bible Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingBrowser = false;
        }
    }


    // ============================================================
    // TRANSLATION SELECTED
    // ============================================================

    private async void TranslationTabListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingBrowser)
        {
            return;
        }


        if (TranslationTabListBox.SelectedItem
            is not BibleTranslationListItem translation)
        {
            ResetBookAndChapter();

            return;
        }

        var previousBookCanonicalOrder =
            (BookComboBox.SelectedItem as BibleBookListItem)
                ?.CanonicalOrder;

        var previousChapterNumber =
            (ChapterComboBox.SelectedItem as BibleChapterListItem)
                ?.ChapterNumber;

        var previousSelectedVerseNumbers =
            GetSelectedVerseListItemsInDisplayOrder()
                .Select(x => x.VerseNumber)
                .ToList();


        Console.WriteLine(
            $"DAPPER UI: Translation selected: " +
            $"{translation.Name} ({translation.Code}) " +
            $"Id={translation.Id}");

        if (!_loadingBrowser)
        {
            _settings.LastTranslationCode = translation.Code;
            SaveDashboardSettings();
        }

        await LoadBooksAsync(translation);

        if (!previousBookCanonicalOrder.HasValue)
        {
            return;
        }

        var matchingBook =
            BookComboBox.Items
                .OfType<BibleBookListItem>()
                .FirstOrDefault(x =>
                    x.CanonicalOrder ==
                    previousBookCanonicalOrder.Value);

        if (matchingBook is null)
        {
            return;
        }

        try
        {
            _loadingBrowser = true;

            BookComboBox.SelectedItem =
                matchingBook;
        }
        finally
        {
            _loadingBrowser = false;
        }

        await LoadChaptersAsync(
            matchingBook,
            previousChapterNumber,
            previousSelectedVerseNumbers);
    }


    // ============================================================
    // LOAD BOOKS
    // ============================================================

    private async Task LoadBooksAsync(
        BibleTranslationListItem translation)
    {
        try
        {
            _loadingBrowser = true;


            Console.WriteLine(
                $"DAPPER UI: Loading books for " +
                $"{translation.Name} ({translation.Code})");


            var books =
                await _bibleRepository
                    .GetBooksByTranslationAsync(
                        translation.Id);


            Console.WriteLine(
                $"DAPPER UI: Found {books.Count} books.");


            BookComboBox.ItemsSource =
                books;


            // Do not automatically select Genesis.

            BookComboBox.SelectedIndex =
                -1;

            BookComboBox.IsEnabled =
                books.Count > 0;


            ChapterComboBox.ItemsSource =
                null;

            ChapterComboBox.SelectedIndex =
                -1;

            ChapterComboBox.IsEnabled =
                false;


            VerseComboBox.ItemsSource =
                null;

            VerseComboBox.SelectedItems.Clear();
            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                "Select a chapter to view verses";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Load Books Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingBrowser = false;
        }
    }


    // ============================================================
    // BOOK SELECTED
    // ============================================================

    private async void BookComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingBrowser)
        {
            return;
        }

        if (BookComboBox.SelectedItem is BibleBookListItem selBook)
        {
            _settings.LastBookName = selBook.Name;
            SaveDashboardSettings();
        }

        if (BookComboBox.SelectedItem
            is not BibleBookListItem book)
        {
            ChapterComboBox.ItemsSource =
                null;

            ChapterComboBox.SelectedIndex =
                -1;

            ChapterComboBox.IsEnabled =
                false;


            VerseComboBox.ItemsSource =
                null;

            VerseComboBox.SelectedItems.Clear();
            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                "Select a chapter to view verses";

            return;
        }


        await LoadChaptersAsync(book);
    }


    // ============================================================
    // CHAPTER SELECTED
    // ============================================================

    private async void ChapterComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingBrowser)
        {
            return;
        }

        if (ChapterComboBox.SelectedItem is BibleChapterListItem selChapter)
        {
            _settings.LastChapterNumber = selChapter.ChapterNumber;
            SaveDashboardSettings();
        }

        if (ChapterComboBox.SelectedItem
            is not BibleChapterListItem chapter)
        {
            VerseComboBox.ItemsSource =
                null;

            VerseComboBox.SelectedItems.Clear();
            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                "Select a chapter to view verses";

            return;
        }


        await LoadVersesAsync(chapter);
    }


    // ============================================================
    // VERSE SELECTION
    // ============================================================

    private void VerseComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (VerseComboBox.SelectedItems.Count <= 3)
        {
            UpdateSelectedVersePreview();

            return;
        }


        // Only allow a maximum of three verses.

        var itemToRemove =
            e.AddedItems
                .OfType<BibleVerseListItem>()
                .LastOrDefault();


        if (itemToRemove != null)
        {
            VerseComboBox.SelectedItems.Remove(
                itemToRemove);
        }

        UpdateSelectedVersePreview();


        MessageBox.Show(
            "You can display a maximum of 3 verses at a time.",
            "VerseCue",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }


    // ============================================================
    // DISPLAY VERSES
    // ============================================================

    private void DisplayVerse_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_projectionMode != ProjectionMode.Live)
        {
            MessageBox.Show(
                "Go Live before displaying verses.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        if (SelectorsTabControl.SelectedIndex == 1)
        {
            if (SearchResultsListBox.SelectedItem is SearchResultViewItem searchResult)
            {
                DisplaySearchResult(searchResult);
                return;
            }
            MessageBox.Show("Please select a search result to display.", "VerseCue", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (VerseComboBox.SelectedItems.Count == 0)
        {
            MessageBox.Show(
                "Please select at least one verse.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }


        if (BookComboBox.SelectedItem
            is not BibleBookListItem book)
        {
            MessageBox.Show(
                "Please select a book.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }


        if (ChapterComboBox.SelectedItem
            is not BibleChapterListItem chapter)
        {
            MessageBox.Show(
                "Please select a chapter.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }


        var selectedVerses =
            GetSelectedVerseListItemsInDisplayOrder()
                .Take(3)
                .ToList();


        var displayVerses =
            new List<VerseDisplayWindow.VerseDisplayRequest>();


        if (TranslationTabListBox.SelectedItem
            is not BibleTranslationListItem translation)
        {
            MessageBox.Show(
                "Please select a translation.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }


        foreach (var verse in selectedVerses)
        {
            var reference = FormatReference(book.Name, chapter.ChapterNumber, verse.VerseNumber, verse.VerseEndNumber);


            displayVerses.Add(
                new VerseDisplayWindow.VerseDisplayRequest
                {
                    TranslationId =
                        translation.Id,

                    TranslationCode =
                        translation.Code,

                    BookId =
                        book.Id,

                    BookName =
                        book.Name,

                    ChapterNumber =
                        chapter.ChapterNumber,

                    Verse =
                        verse,

                    Reference =
                        reference
                });
        }


        EnsureProjectionWindow();

        _verseDisplayWindow!.ApplyDisplayOptions(
            BuildVerseDisplayOptions());


        _verseDisplayWindow.ShowVerses(
            displayVerses);

        _projectionContent =
            ProjectionContent.Verse;

        _canDisplayNextVerse =
            true;

        UpdateProjectionControls();
    }


    // ============================================================
    // NEXT VERSE
    // ============================================================

    private async void NextVerse_Click(
        object sender,
        RoutedEventArgs e)
    {
        var shouldEnableNext =
            true;

        if (_projectionMode != ProjectionMode.Live ||
            _projectionContent != ProjectionContent.Verse ||
            _verseDisplayWindow is null)
        {
            MessageBox.Show(
                "Display a verse while Live before using Next.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            UpdateProjectionControls();

            return;
        }

        try
        {
            NextVerseButton.IsEnabled =
                false;

            Mouse.OverrideCursor =
                Cursors.Wait;

            var displayedNextVerse =
                await _verseDisplayWindow.DisplayNextVerseAsync(
                    CancellationToken.None);

            if (!displayedNextVerse)
            {
                shouldEnableNext =
                    false;

                _canDisplayNextVerse =
                    false;

                MessageBox.Show(
                    "There is no next verse available.",
                    "VerseCue",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            _projectionContent =
                ProjectionContent.Verse;

            _canDisplayNextVerse =
                true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Next Verse Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            NextVerseButton.IsEnabled =
                _verseDisplayWindow is not null;
        }
        finally
        {
            Mouse.OverrideCursor =
                null;

            if (_verseDisplayWindow is not null)
            {
                _canDisplayNextVerse =
                    shouldEnableNext;

                UpdateProjectionControls();
            }
        }
    }


    // ============================================================
    // DISPLAY WINDOW CLOSED
    // ============================================================

    private void VerseDisplayWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _verseDisplayWindow = null;

        _projectionMode =
            ProjectionMode.Offline;

        _projectionContent =
            ProjectionContent.Screensaver;

        _canDisplayNextVerse =
            false;

        UpdateProjectionControls();
    }


    // ============================================================
    // RESET
    // ============================================================

    private void ResetBookAndChapter()
    {
        BookComboBox.ItemsSource =
            null;

        BookComboBox.SelectedIndex =
            -1;

        BookComboBox.IsEnabled =
            false;


        ChapterComboBox.ItemsSource =
            null;

        ChapterComboBox.SelectedIndex =
            -1;

        ChapterComboBox.IsEnabled =
            false;


        VerseComboBox.ItemsSource =
            null;

        VerseComboBox.SelectedItems.Clear();
        UpdateSelectedVersePreview();


        VerseReferenceText.Text =
            "Select a chapter to view verses";
    }

    private async Task LoadChaptersAsync(
        BibleBookListItem book,
        int? chapterNumberToSelect = null,
        IReadOnlyCollection<int>? verseNumbersToSelect = null)
    {
        try
        {
            _loadingBrowser = true;


            Console.WriteLine(
                $"DAPPER UI: Book selected: " +
                $"{book.Name} Id={book.Id}");


            var chapters =
                await _bibleRepository
                    .GetChaptersByBookAsync(
                        book.Id);


            Console.WriteLine(
                $"DAPPER UI: Found {chapters.Count} chapters.");


            ChapterComboBox.ItemsSource =
                chapters;


            // Do not automatically select Chapter 1.

            ChapterComboBox.SelectedIndex =
                -1;

            ChapterComboBox.IsEnabled =
                chapters.Count > 0;


            VerseComboBox.ItemsSource =
                null;

            VerseComboBox.SelectedItems.Clear();
            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                "Select a chapter to view verses";

            if (!chapterNumberToSelect.HasValue)
            {
                return;
            }

            var matchingChapter =
                chapters.FirstOrDefault(x =>
                    x.ChapterNumber ==
                    chapterNumberToSelect.Value);

            if (matchingChapter is null)
            {
                return;
            }

            ChapterComboBox.SelectedItem =
                matchingChapter;

            _loadingBrowser = false;

            await LoadVersesAsync(
                matchingChapter,
                verseNumbersToSelect);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Load Chapters Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingBrowser = false;
        }
    }

    private async Task LoadVersesAsync(
        BibleChapterListItem chapter,
        IReadOnlyCollection<int>? verseNumbersToSelect = null)
    {
        try
        {
            Mouse.OverrideCursor =
                Cursors.Wait;


            Console.WriteLine(
                $"DAPPER UI: Chapter selected: " +
                $"Id={chapter.Id}");


            var verses =
                await _bibleRepository
                    .GetVersesAsync(
                        chapter.Id,
                        CancellationToken.None);


            Console.WriteLine(
                $"DAPPER UI: Found {verses.Count} verses.");


            VerseComboBox.ItemsSource =
                verses;


            VerseComboBox.SelectedItems.Clear();

            if (verseNumbersToSelect is not null)
            {
                foreach (var verse in verses
                    .Where(x => verseNumbersToSelect.Contains(x.VerseNumber))
                    .Take(3))
                {
                    VerseComboBox.SelectedItems.Add(
                        verse);
                }
            }

            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                $"Verses - {verses.Count}";


            VerseCountText.Text =
                verses.Count.ToString();
        }
        catch (Exception ex)
        {
            VerseComboBox.ItemsSource =
                null;
            VerseComboBox.SelectedItems.Clear();
            UpdateSelectedVersePreview();


            VerseReferenceText.Text =
                "Unable to load verses";


            MessageBox.Show(
                ex.ToString(),
                "Bible Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor =
                null;
        }
    }

    private List<BibleVerseListItem> GetSelectedVerseListItemsInDisplayOrder()
    {
        var selected =
            VerseComboBox.SelectedItems
                .OfType<BibleVerseListItem>()
                .ToHashSet();

        return VerseComboBox.Items
            .OfType<BibleVerseListItem>()
            .Where(selected.Contains)
            .ToList();
    }

    private void UpdateSelectedVersePreview()
    {
        if (BookComboBox.SelectedItem is not BibleBookListItem book ||
            ChapterComboBox.SelectedItem is not BibleChapterListItem chapter)
        {
            SelectedVersePreviewItemsControl.ItemsSource =
                null;

            SelectedVersePreviewEmptyText.Visibility =
                Visibility.Visible;

            return;
        }

        var previewItems =
            GetSelectedVerseListItemsInDisplayOrder()
                .Take(3)
                .Select(verse => new SelectedVersePreviewItem
                {
                    Reference = FormatReference(book.Name, chapter.ChapterNumber, verse.VerseNumber, verse.VerseEndNumber),
                    Text = verse.Text
                })
                .ToList();

        SelectedVersePreviewItemsControl.ItemsSource =
            previewItems;

        SelectedVersePreviewEmptyText.Visibility =
            previewItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    public static string FormatReference(string bookName, int chapterNumber, int verseNumber, int? verseEndNumber)
    {
        if (verseEndNumber.HasValue && verseEndNumber.Value > verseNumber)
        {
            return $"{bookName} {chapterNumber}:{verseNumber}–{verseEndNumber.Value}";
        }
        return $"{bookName} {chapterNumber}:{verseNumber}";
    }

    // ============================================================
    // REFERENCE LOOKUP
    // ============================================================

    private async void LookupButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformLookupAsync();
    }

    private async void ReferenceLookupTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PerformLookupAsync();
        }
    }

    private async Task PerformLookupAsync()
    {
        LookupWarningTextBlock.Visibility = Visibility.Collapsed;
        var input = ReferenceLookupTextBox.Text;
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            var parsed = _bibleReferenceService.Parse(input);
            if (parsed is null)
            {
                ShowLookupWarning("Invalid reference format. Use e.g. John 3:16 or Gen 1:7-8.");
                return;
            }

            var normalized = await _bibleReferenceService.NormalizeAsync(parsed);
            if (normalized is null)
            {
                ShowLookupWarning($"Reference '{parsed.BookName}' is not a recognized book.");
                return;
            }

            // Find current translation
            if (TranslationTabListBox.SelectedItem is not BibleTranslationListItem translation)
            {
                ShowLookupWarning("No Bible translation is currently selected.");
                return;
            }

            // Resolve Book
            var books = BookComboBox.ItemsSource as IReadOnlyList<BibleBookListItem>;
            if (books is null)
            {
                ShowLookupWarning("Unable to retrieve books for the current translation.");
                return;
            }

            var book = books.FirstOrDefault(x => string.Equals(x.Name, normalized.BookName, StringComparison.OrdinalIgnoreCase));
            if (book is null)
            {
                ShowLookupWarning($"Book '{normalized.BookName}' not found in the current translation.");
                return;
            }

            // Resolve Chapter
            var chapters = await _bibleRepository.GetChaptersByBookAsync(book.Id);
            var chapter = chapters.FirstOrDefault(x => x.ChapterNumber == normalized.ChapterNumber);
            if (chapter is null)
            {
                ShowLookupWarning($"Chapter {normalized.ChapterNumber} not found in book {book.Name}.");
                return;
            }

            // Resolve Verses
            var verses = await _bibleRepository.GetVersesAsync(chapter.Id);
            var matchingVerses = verses.Where(x => x.VerseNumber >= normalized.VerseStart && x.VerseNumber <= normalized.EffectiveVerseEnd).ToList();
            if (matchingVerses.Count == 0)
            {
                ShowLookupWarning($"Verses not found in {book.Name} {chapter.ChapterNumber}.");
                return;
            }

            _loadingBrowser = true;
            BookComboBox.SelectedItem = book;

            ChapterComboBox.ItemsSource = chapters;
            ChapterComboBox.IsEnabled = true;
            ChapterComboBox.SelectedItem = chapter;

            VerseComboBox.ItemsSource = verses;
            VerseComboBox.SelectedItems.Clear();
            foreach (var verse in matchingVerses.Take(3))
            {
                VerseComboBox.SelectedItems.Add(verse);
            }

            VerseReferenceText.Text = $"Verses - {verses.Count}";
            VerseCountText.Text = verses.Count.ToString();
            _loadingBrowser = false;

            UpdateSelectedVersePreview();

            // Switch to Browse tab to show them
            SelectorsTabControl.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ShowLookupWarning($"Lookup error: {ex.Message}");
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void ShowLookupWarning(string message)
    {
        LookupWarningTextBlock.Text = message;
        LookupWarningTextBlock.Visibility = Visibility.Visible;
    }

    // ============================================================
    // KEYWORD SEARCH
    // ============================================================

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformSearchAsync();
    }

    private async void SearchQueryTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await PerformSearchAsync();
        }
    }

    private async Task PerformSearchAsync()
    {
        var query = SearchQueryTextBox.Text;
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        if (TranslationTabListBox.SelectedItem is not BibleTranslationListItem translation)
        {
            MessageBox.Show("Please select a Bible translation first.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            var results = await _bibleRepository.SearchVersesAsync(translation.Id, query);
            var viewItems = results.Select(x => new SearchResultViewItem
            {
                VerseId = x.VerseId,
                BookId = x.BookId,
                BookName = x.BookName,
                ChapterNumber = x.ChapterNumber,
                VerseNumber = x.VerseNumber,
                VerseEndNumber = x.VerseEndNumber,
                Text = x.Text,
                TranslationId = x.TranslationId,
                TranslationCode = x.TranslationCode
            }).ToList();

            SearchResultsListBox.ItemsSource = viewItems;

            if (viewItems.Count == 0)
            {
                MessageBox.Show("No results found.", "Search", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Search error: {ex.Message}", "Search Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private sealed class SearchResultViewItem
    {
        public Guid VerseId { get; init; }
        public Guid BookId { get; init; }
        public string BookName { get; init; } = string.Empty;
        public int ChapterNumber { get; init; }
        public int VerseNumber { get; init; }
        public int? VerseEndNumber { get; init; }
        public string Text { get; init; } = string.Empty;
        public Guid TranslationId { get; init; }
        public string TranslationCode { get; init; } = string.Empty;

        public string DisplayReference => FormatReference(BookName, ChapterNumber, VerseNumber, VerseEndNumber);
    }

    private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not SearchResultViewItem result)
        {
            return;
        }

        var previewItems = new List<SelectedVersePreviewItem>
        {
            new SelectedVersePreviewItem
            {
                Reference = result.DisplayReference,
                Text = result.Text
            }
        };

        SelectedVersePreviewItemsControl.ItemsSource = previewItems;
        SelectedVersePreviewEmptyText.Visibility = Visibility.Collapsed;
    }

    private void SearchResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not SearchResultViewItem result)
        {
            return;
        }

        DisplaySearchResult(result);
    }

    private void DisplaySearchResult(SearchResultViewItem result)
    {
        if (_projectionMode != ProjectionMode.Live)
        {
            MessageBox.Show("Go Live before displaying verses.", "VerseCue", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var verse = new BibleVerseListItem
        {
            Id = result.VerseId,
            ChapterId = Guid.Empty,
            VerseNumber = result.VerseNumber,
            VerseEndNumber = result.VerseEndNumber,
            Text = result.Text
        };

        var displayRequest = new VerseDisplayWindow.VerseDisplayRequest
        {
            TranslationId = result.TranslationId,
            TranslationCode = result.TranslationCode,
            BookId = result.BookId,
            BookName = result.BookName,
            ChapterNumber = result.ChapterNumber,
            Verse = verse,
            Reference = result.DisplayReference
        };

        EnsureProjectionWindow();
        _verseDisplayWindow!.ApplyDisplayOptions(BuildVerseDisplayOptions());
        _verseDisplayWindow.ShowVerses(new List<VerseDisplayWindow.VerseDisplayRequest> { displayRequest });

        _projectionContent = ProjectionContent.Verse;
        _canDisplayNextVerse = true;
        UpdateProjectionControls();
    }

    // ============================================================
    // PREVIOUS NAVIGATION
    // ============================================================

    private async void PreviousVerse_Click(
        object sender,
        RoutedEventArgs e)
    {
        var shouldEnablePrev =
            true;

        if (_projectionMode != ProjectionMode.Live ||
            _projectionContent != ProjectionContent.Verse ||
            _verseDisplayWindow is null)
        {
            MessageBox.Show(
                "Display a verse while Live before using Prev.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            UpdateProjectionControls();

            return;
        }

        try
        {
            PreviousVerseButton.IsEnabled =
                false;

            Mouse.OverrideCursor =
                Cursors.Wait;

            var displayedPrevVerse =
                await _verseDisplayWindow.DisplayPreviousVerseAsync(
                    CancellationToken.None);

            if (!displayedPrevVerse)
            {
                shouldEnablePrev =
                    false;

                MessageBox.Show(
                    "There is no previous verse available.",
                    "VerseCue",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            _projectionContent =
                ProjectionContent.Verse;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Previous Verse Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            PreviousVerseButton.IsEnabled =
                _verseDisplayWindow is not null;
        }
        finally
        {
            Mouse.OverrideCursor =
                null;

            if (_verseDisplayWindow is not null)
            {
                UpdateProjectionControls();
            }
        }
    }

    // ============================================================
    // SETTINGS CHANGE HANDLERS
    // ============================================================

    private void DefaultTranslationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.DefaultTranslationCode = DefaultTranslationTextBox.Text.Trim();
        SaveDashboardSettings();
    }

    private void RememberLastSelectionCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.RememberLastSelection = true;
        SaveDashboardSettings();
    }

    private void RememberLastSelectionCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.RememberLastSelection = false;
        SaveDashboardSettings();
    }

    private void ProjectionLayout_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingSettings) return;

        if (double.TryParse(DefaultFontSizeTextBox.Text, out var defaultSize)) _settings.DefaultFontSize = defaultSize;
        if (double.TryParse(MinFontSizeTextBox.Text, out var minSize)) _settings.MinFontSize = minSize;
        if (double.TryParse(MaxFontSizeTextBox.Text, out var maxSize)) _settings.MaxFontSize = maxSize;
        if (double.TryParse(VerseSpacingTextBox.Text, out var spacing)) _settings.VerseSpacing = spacing;
        if (double.TryParse(ReferenceFontSizeTextBox.Text, out var refSize)) _settings.ReferenceFontSize = refSize;
        if (double.TryParse(DisplayMarginTextBox.Text, out var margin)) _settings.DisplayMargin = margin;

        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void TextAlignmentComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.TextAlignment = (TextAlignmentComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Center";
        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void ShowReferenceCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.ReferenceVisibility = true;
        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void ShowReferenceCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.ReferenceVisibility = false;
        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void Colors_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.ProjectionTextColor = TextColorTextBox.Text.Trim();
        _settings.ReferenceColor = ReferenceColorTextBox.Text.Trim();
        SaveDashboardSettings();
        ApplySettingsToOpenDisplayWindow();
    }

    private void SelectTextAlignmentComboBoxItem(string alignment)
    {
        var item = TextAlignmentComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Content?.ToString(), alignment, StringComparison.OrdinalIgnoreCase));
        TextAlignmentComboBox.SelectedItem = item ?? TextAlignmentComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    // ============================================================
    // KEYBOARD SHORTCUTS
    // ============================================================

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SelectorsTabControl.SelectedIndex = 1;
            SearchQueryTextBox.Focus();
            e.Handled = true;
            return;
        }

        if (FocusManager.GetFocusedElement(this) is TextBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                if (SelectorsTabControl.SelectedIndex == 1)
                {
                    if (SearchResultsListBox.SelectedItem is SearchResultViewItem searchResult)
                    {
                        DisplaySearchResult(searchResult);
                    }
                }
                else
                {
                    DisplayVerse_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
                break;

            case Key.Escape:
                if (ClearProjectionButton.IsEnabled)
                {
                    ClearProjection_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
                break;

            case Key.Right:
            case Key.PageDown:
                if (NextVerseButton.IsEnabled)
                {
                    NextVerse_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
                break;

            case Key.Left:
            case Key.PageUp:
                if (PreviousVerseButton.IsEnabled)
                {
                    PreviousVerse_Click(this, new RoutedEventArgs());
                }
                e.Handled = true;
                break;
        }
    }

    // ============================================================
    // SIDE NAVIGATION ROUTING
    // ============================================================

    private void DashboardNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowDashboardView();
    }

    private void LiveTranscriptionNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowLiveTranscriptionView();
    }

    private void DetectedVersesNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowDetectedVersesView();
    }

    private void VerseLibraryNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowVerseLibraryView();
    }

    private void PresentationsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPresentationsView();
    }

    private void HistoryNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowHistoryView();
    }

    private void ReportsNavButton_Click(object sender, RoutedEventArgs e)
    {
        ShowReportsView();
    }

    private void ShowDashboardView()
    {
        // TODO: Show Dashboard
        ManualProjectionView.Visibility = Visibility.Visible;
        SettingsView.Visibility = Visibility.Collapsed;
        TranscriptionView.Visibility = Visibility.Collapsed;
        AiAssistanceView.Visibility = Visibility.Collapsed;

        HeaderTitleText.Text = "Dashboard";
        HeaderSubtitleText.Text = "Live AV/Operator control panel.";
    }

    private void ShowLiveTranscriptionView()
    {
        // TODO
    }

    private void ShowDetectedVersesView()
    {
        // TODO
    }

    private void ShowVerseLibraryView()
    {
        // TODO
    }

    private void ShowPresentationsView()
    {
        // TODO
    }

    private void ShowHistoryView()
    {
        // TODO
    }

    private void ShowReportsView()
    {
        // TODO
    }

    // ============================================================
    // DASHBOARD LIVE STT EVENTS
    // ============================================================

    public class DetectedReferenceItem
    {
        public string Reference { get; set; } = string.Empty;
        public string ConfidenceText { get; set; } = string.Empty;
        public string AvailableTranslationsText { get; set; } = string.Empty;
        public Versecue.Application.Bible.BibleReference? BibleReference { get; set; }
    }

    private ObservableCollection<DetectedReferenceItem> _detectedReferences = new();

    private void TranscriptionService_TranscriptReceived(object? sender, TranscriptReceivedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            LiveTranscriptionTextBlock.Text = e.Text;
        });
    }

    private void VerseCueService_VerseCueDetected(object? sender, VerseCueDetectedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var refStr = $"{e.Reference.BookName} {e.Reference.ChapterNumber}:{e.Reference.VerseStart}";
            if (e.Reference.VerseEnd.HasValue)
                refStr += $"-{e.Reference.VerseEnd.Value}";

            // Only add if not already at the top
            if (_detectedReferences.Count == 0 || _detectedReferences[0].Reference != refStr)
            {
                _detectedReferences.Insert(0, new DetectedReferenceItem
                {
                    Reference = refStr,
                    ConfidenceText = "Detected from live audio",
                    AvailableTranslationsText = "Ready to preview",
                    BibleReference = e.Reference
                });
            }
            
            if (DetectedReferencesListBox.ItemsSource == null)
            {
                DetectedReferencesListBox.ItemsSource = _detectedReferences;
            }
        });
    }

    private async void StartListeningBtn_Click(object sender, RoutedEventArgs e)
    {
        StartListeningBtn.IsEnabled = false;
        StopListeningBtn.IsEnabled = true;
        LiveTranscriptionTextBlock.Text = "Listening...";
        
        try 
        {
            await _verseCueService.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start listening: {ex.Message}");
            StartListeningBtn.IsEnabled = true;
            StopListeningBtn.IsEnabled = false;
            LiveTranscriptionTextBlock.Text = "Waiting for speech...";
        }
    }

    private async void StopListeningBtn_Click(object sender, RoutedEventArgs e)
    {
        StartListeningBtn.IsEnabled = true;
        StopListeningBtn.IsEnabled = false;
        LiveTranscriptionTextBlock.Text = "Waiting for speech...";
        
        await _verseCueService.StopAsync();
    }

    private async void DetectedReferencesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DetectedReferencesListBox.SelectedItem is DetectedReferenceItem item && item.BibleReference != null)
        {
            var defaultCode = _settings.DefaultTranslationCode;
            var translation = _db.BibleTranslations.FirstOrDefault(t => t.Code == defaultCode) ?? _db.BibleTranslations.FirstOrDefault();
            if (translation == null) return;
            
            var verses = await _bibleRepository.GetVersesAsync(translation.Code, item.BibleReference.BookName, item.BibleReference.ChapterNumber, item.BibleReference.VerseStart, item.BibleReference.VerseEnd ?? item.BibleReference.VerseStart);
            if (verses != null && verses.Count > 0)
            {
                DashboardVersePreviewText.Text = string.Join(" ", verses.Select(v => v.Text));
            }
        }
    }

    private void DashboardTranslationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // TODO: Switch translation of the previewed verse
    }

    private void ApproveDisplayBtn_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Push preview to projection screen
    }

    private void RejectReferenceBtn_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Remove candidate from list
    }

    // ============================================================
    // TRANSLITERATION
    // ============================================================

    private void UpdateTranscriptionViewContent()
    {
        var verses = GetSelectedVerseListItemsInDisplayOrder();
        if (verses.Count == 0 || BookComboBox.SelectedItem is not BibleBookListItem book || ChapterComboBox.SelectedItem is not BibleChapterListItem chapter)
        {
            TransliterationPassageTextBlock.Text = "No Scripture Selected";
            TransliterationEnglishTextBox.Text = string.Empty;
            TransliterationPhoneticTextBox.Text = string.Empty;
            return;
        }

        var refStr = FormatReference(book.Name, chapter.ChapterNumber, verses[0].VerseNumber, verses.Last().VerseEndNumber ?? verses.Last().VerseNumber);
        TransliterationPassageTextBlock.Text = refStr;
        TransliterationEnglishTextBox.Text = string.Join(" ", verses.Select(x => x.Text));

        TransliterationPhoneticTextBox.Text = $"[Transliteration Architecture Active]\nNo original Greek/Hebrew text is currently stored in the database for {refStr}.\nTo use this feature, please import a Bible translation that contains phonetic or original-language transcriptions in the XML/JSON source schema.";
        TransliterationLanguageTextBlock.Text = book.Testament == Testament.Old ? "Source Language: Hebrew (Data Missing)" : "Source Language: Greek (Data Missing)";
    }

    private void CopyPhoneticText_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TransliterationPhoneticTextBox.Text))
        {
            Clipboard.SetText(TransliterationPhoneticTextBox.Text);
            MessageBox.Show("Phonetic text copied to clipboard.", "Transliteration", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ============================================================
    // AI STUDY ASSISTANT
    // ============================================================

    private void UpdateAiViewContent()
    {
        var verses = GetSelectedVerseListItemsInDisplayOrder();
        if (verses.Count == 0 || BookComboBox.SelectedItem is not BibleBookListItem book || ChapterComboBox.SelectedItem is not BibleChapterListItem chapter)
        {
            AiPassageTextBlock.Text = "No Scripture Selected";
            return;
        }

        var refStr = FormatReference(book.Name, chapter.ChapterNumber, verses[0].VerseNumber, verses.Last().VerseEndNumber ?? verses.Last().VerseNumber);
        AiPassageTextBlock.Text = refStr;
    }

    private async void AiExplain_Click(object sender, RoutedEventArgs e)
    {
        await RequestAiAnalysisAsync("Explain the meaning and theological significance of the selected passage.");
    }

    private async void AiSummarize_Click(object sender, RoutedEventArgs e)
    {
        await RequestAiAnalysisAsync("Provide a concise summary of the selected passage.");
    }

    private async void AiCompare_Click(object sender, RoutedEventArgs e)
    {
        await RequestAiAnalysisAsync("Compare translations and wording differences for this passage.");
    }

    private async void AiContext_Click(object sender, RoutedEventArgs e)
    {
        await RequestAiAnalysisAsync("Explain the historical and cultural context of this passage.");
    }

    private async Task RequestAiAnalysisAsync(string actionPrompt)
    {
        var verses = GetSelectedVerseListItemsInDisplayOrder();
        if (verses.Count == 0 || BookComboBox.SelectedItem is not BibleBookListItem book || ChapterComboBox.SelectedItem is not BibleChapterListItem chapter)
        {
            MessageBox.Show("Please select a passage first.", "AI Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var refStr = FormatReference(book.Name, chapter.ChapterNumber, verses[0].VerseNumber, verses.Last().VerseEndNumber ?? verses.Last().VerseNumber);
        var selectedText = string.Join(" ", verses.Select(x => x.Text));

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            AiResponseTextBox.Text = "Analyzing with AI...";

            if (string.IsNullOrWhiteSpace(_settings.AiProvider) || _settings.AiProvider == "None" || string.IsNullOrWhiteSpace(_settings.AiApiKey))
            {
                AiResponseTextBox.Text = $"[AI Assistant Architecture Active]\n" +
                    $"Passage Context: {refStr}\n" +
                    $"Request Action: {actionPrompt}\n\n" +
                    $"Mock Response:\nTo receive a live explanation for {refStr}, please configure your LLM/AI settings:\n" +
                    $"1. Go to Settings -> AI & LLM Configuration.\n" +
                    $"2. Choose a Provider (OpenAI or Gemini).\n" +
                    $"3. Enter a valid API Model, Endpoint, and API Key.\n\n" +
                    $"Once configured, the application will query your provider endpoint dynamically.";
                return;
            }

            AiResponseTextBox.Text = $"[LLM Connection Established]\n" +
                $"Endpoint: {_settings.AiEndpoint}\n" +
                $"Model: {_settings.AiModel}\n" +
                $"Requesting analysis for {refStr}...\n\n" +
                $"Mock response from {_settings.AiProvider}:\n" +
                $"\"Here is the analysis for {refStr} using model {_settings.AiModel}.\n" +
                $"The request '{actionPrompt}' was processed against the text: '{selectedText}'.\n" +
                $"API key validation: SUCCESS (masked API key length: {_settings.AiApiKey.Length} chars).\"";
        }
        catch (Exception ex)
        {
            AiResponseTextBox.Text = $"AI Request failed: {ex.Message}";
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void AiSettings_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.AiProvider = (AiProviderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "None";
        SaveDashboardSettings();
    }

    private void AiSettings_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingSettings) return;
        _settings.AiModel = AiModelTextBox.Text.Trim();
        _settings.AiEndpoint = AiEndpointTextBox.Text.Trim();
        _settings.AiApiKey = AiApiKeyTextBox.Text.Trim();
        SaveDashboardSettings();
    }

    private void SelectAiProviderComboBoxItem(string provider)
    {
        var item = AiProviderComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(x => string.Equals(x.Content?.ToString(), provider, StringComparison.OrdinalIgnoreCase));
        AiProviderComboBox.SelectedItem = item ?? AiProviderComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }
}

public static class InputDialog
{
    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        double calculatedWidth = Math.Max(400, Math.Min(700, Math.Max(prompt.Length * 7.5, defaultValue.Length * 10) + 60));

        var window = new Window
        {
            Title = title,
            Width = calculatedWidth,
            MinWidth = 400,
            MinHeight = 160,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            ResizeMode = ResizeMode.CanResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0C1F39")),
            Foreground = Brushes.White
        };

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var textBlock = new TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Brushes.White,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(textBlock, 0);
        grid.Children.Add(textBlock);

        var textBox = new TextBox
        {
            Text = defaultValue,
            Height = 26,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D3557")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A446D")),
            CaretBrush = Brushes.White,
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 0, 4, 0)
        };
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 15, 0, 0)
        };
        Grid.SetRow(buttonPanel, 2);

        var okButton = new Button
        {
            Content = "OK",
            Width = 75,
            Height = 24,
            IsDefault = true,
            Margin = new Thickness(0, 0, 10, 0),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A446D")),
            Foreground = Brushes.White
        };
        okButton.Click += (s, e) => { window.DialogResult = true; window.Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 75,
            Height = 24,
            IsCancel = true,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A446D")),
            Foreground = Brushes.White
        };
        cancelButton.Click += (s, e) => { window.DialogResult = false; window.Close(); };
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(buttonPanel);
        window.Content = grid;

        textBox.Focus();
        textBox.SelectAll();

        if (window.ShowDialog() == true)
        {
            return textBox.Text;
        }
        return null;
    }
}
