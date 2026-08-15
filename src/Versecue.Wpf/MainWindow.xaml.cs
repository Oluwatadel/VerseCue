using System.IO;
using System.Text.Json;
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
        public string DashboardTheme { get; set; } = "VerseCue Dark";

        public string VerseDisplayWallpaper { get; set; } = "Classic Black";

        public string ImportedWallpaperPath { get; set; } = string.Empty;

        public string SelectedWallpaperId { get; set; } = "builtin:classic-black";

        public List<ImportedWallpaperSetting> ImportedWallpapers { get; set; } = [];

        public string SelectedScreensaverId { get; set; } = "builtin:versecue-default";

        public List<ImportedScreensaverSetting> ImportedScreensavers { get; set; } = [];
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

    public MainWindow(
        IBibleImportService bibleImportService,
        VersecueDbContext db,
        IBibleRepository bibleRepository)
    {
        InitializeComponent();

        _bibleImportService = bibleImportService;
        _db = db;
        _bibleRepository = bibleRepository;

        Loaded += MainWindow_Loaded;
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

    private void DashboardNavButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowManualProjectionView();
    }

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
                true
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

            if (translations.Count > 0)
            {
                TranslationTabListBox.SelectedIndex =
                    0;

                await LoadBooksAsync(
                    translations[0]);
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
            var reference = verse.VerseEndNumber.HasValue
                ? $"{book.Name} {chapter.ChapterNumber}:{verse.VerseNumber}–{verse.VerseEndNumber}"
                : $"{book.Name} {chapter.ChapterNumber}:{verse.VerseNumber}";


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
                    Reference = verse.VerseEndNumber.HasValue
                        ? $"{book.Name} {chapter.ChapterNumber}:{verse.VerseNumber}–{verse.VerseEndNumber}"
                        : $"{book.Name} {chapter.ChapterNumber}:{verse.VerseNumber}",

                    Text =
                        verse.Text
                })
                .ToList();

        SelectedVersePreviewItemsControl.ItemsSource =
            previewItems;

        SelectedVersePreviewEmptyText.Visibility =
            previewItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
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
