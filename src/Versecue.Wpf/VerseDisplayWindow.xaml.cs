using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Versecue.Application.Interfaces;
using Versecue.Application.Models.Bible;

namespace Versecue.Wpf;

public partial class VerseDisplayWindow : Window
{
    private readonly IBibleRepository _bibleRepository;
    private VerseNavigationCursor? _navigationCursor;
    private readonly DispatcherTimer _gifTimer;
    private List<BitmapFrame> _gifFrames = [];
    private int _gifFrameIndex;

    public VerseDisplayWindow(
        IBibleRepository bibleRepository)
    {
        InitializeComponent();

        _bibleRepository = bibleRepository;

        _gifTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromMilliseconds(120)
            };

        _gifTimer.Tick +=
            GifTimer_Tick;
    }

    private sealed class VerseDisplayItem
    {
        public BibleVerseListItem Verse { get; init; } = null!;

        public string Reference { get; init; } = string.Empty;
    }

    public sealed class VerseDisplayOptions
    {
        public string WallpaperName { get; init; } = "Classic Black";

        public string ImportedWallpaperPath { get; init; } = string.Empty;

        public string ScreensaverName { get; init; } = "VerseCue Default";

        public string ScreensaverPath { get; init; } = string.Empty;

        public string ScreensaverMediaKind { get; init; } = "default";

        public bool ShowVerseCueWatermark { get; init; } = true;
    }

    public sealed class VerseDisplayRequest
    {
        public Guid TranslationId { get; init; }

        public string TranslationCode { get; init; } = string.Empty;

        public Guid BookId { get; init; }

        public string BookName { get; init; } = string.Empty;

        public int ChapterNumber { get; init; }

        public BibleVerseListItem Verse { get; init; } = null!;

        public string Reference { get; init; } = string.Empty;
    }

    private sealed class VerseNavigationCursor
    {
        public Guid TranslationId { get; init; }

        public string TranslationCode { get; init; } = string.Empty;

        public Guid BookId { get; init; }

        public string BookName { get; init; } = string.Empty;

        public int ChapterNumber { get; init; }

        public int VerseNumber { get; init; }

        public Guid VerseId { get; init; }
    }

    public void ShowVerses(
        List<VerseDisplayRequest> verses)
    {
        if (verses == null || verses.Count == 0)
        {
            return;
        }

        var displayItems =
            verses
                .Take(3)
                .Select(x => new VerseDisplayItem
                {
                    Verse = x.Verse,
                    Reference = x.Reference
                })
                .ToList();

        HideScreensaverMedia();

        var lastDisplayedVerse =
            verses
                .Take(3)
                .Last();

        _navigationCursor =
            new VerseNavigationCursor
            {
                TranslationId =
                    lastDisplayedVerse.TranslationId,

                TranslationCode =
                    lastDisplayedVerse.TranslationCode,

                BookId =
                    lastDisplayedVerse.BookId,

                BookName =
                    lastDisplayedVerse.BookName,

                ChapterNumber =
                    lastDisplayedVerse.ChapterNumber,

                VerseNumber =
                    lastDisplayedVerse.Verse.VerseNumber,

                VerseId =
                    lastDisplayedVerse.Verse.Id
            };

        VerseItemsControl.ItemsSource = displayItems;

        if (!IsVisible)
        {
            Show();
        }
    }

    public void ApplyDisplayOptions(
        VerseDisplayOptions options)
    {
        HideScreensaverMedia();

        VerseCueWatermark.Visibility =
            options.ShowVerseCueWatermark
                ? Visibility.Visible
                : Visibility.Collapsed;

        DisplayRoot.Background =
            BuildWallpaperBrush(options);

        WallpaperOverlay.Background =
            options.WallpaperName == "Classic Black"
                ? Brushes.Transparent
                : new SolidColorBrush(
                    Color.FromArgb(122, 0, 0, 0));
    }

    public void ShowScreensaver(
        VerseDisplayOptions options)
    {
        VerseItemsControl.ItemsSource =
            null;

        _navigationCursor =
            null;

        VerseCueWatermark.Visibility =
            options.ShowVerseCueWatermark
                ? Visibility.Visible
                : Visibility.Collapsed;

        DisplayRoot.Background =
            new LinearGradientBrush(
                Color.FromRgb(3, 7, 18),
                Color.FromRgb(15, 23, 42),
                35);

        WallpaperOverlay.Background =
            Brushes.Transparent;

        ShowScreensaverMedia(
            options);

        if (!IsVisible)
        {
            Show();
        }
    }

    private void ScreensaverMediaElement_MediaEnded(
        object sender,
        RoutedEventArgs e)
    {
        ScreensaverMediaElement.Position =
            TimeSpan.Zero;

        ScreensaverMediaElement.Play();
    }

    public async Task<bool> DisplayNextVerseAsync(
        CancellationToken cancellationToken = default)
    {
        if (_navigationCursor is null)
        {
            return false;
        }

        var nextVerse =
            await _bibleRepository.GetNextVerseAsync(
                _navigationCursor.TranslationId,
                _navigationCursor.VerseId,
                cancellationToken);

        if (nextVerse is null)
        {
            return false;
        }

        ShowVerses(
            [
                new VerseDisplayRequest
                {
                    TranslationId =
                        nextVerse.TranslationId,

                    TranslationCode =
                        nextVerse.TranslationCode,

                    BookId =
                        nextVerse.BookId,

                    BookName =
                        nextVerse.BookName,

                    ChapterNumber =
                        nextVerse.ChapterNumber,

                    Verse =
                        nextVerse.Verse,

                    Reference = nextVerse.Verse.VerseEndNumber.HasValue
                        ? $"{nextVerse.BookName} {nextVerse.ChapterNumber}:{nextVerse.Verse.VerseNumber}–{nextVerse.Verse.VerseEndNumber}"
                        : $"{nextVerse.BookName} {nextVerse.ChapterNumber}:{nextVerse.Verse.VerseNumber}"
                }
            ]);

        return true;
    }

    private static System.Windows.Media.Brush BuildWallpaperBrush(
        VerseDisplayOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ImportedWallpaperPath) &&
            File.Exists(options.ImportedWallpaperPath))
        {
            var bitmap =
                new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption =
                BitmapCacheOption.OnLoad;
            bitmap.UriSource =
                new Uri(
                    options.ImportedWallpaperPath,
                    UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            return new ImageBrush(bitmap)
            {
                Stretch =
                    Stretch.UniformToFill,

                AlignmentX =
                    AlignmentX.Center,

                AlignmentY =
                    AlignmentY.Center
            };
        }

        if (options.WallpaperName == "Deep Ocean")
        {
            return new LinearGradientBrush(
                Color.FromRgb(4, 47, 74),
                Color.FromRgb(3, 7, 18),
                35);
        }

        if (options.WallpaperName == "Warm Chapel")
        {
            return new LinearGradientBrush(
                Color.FromRgb(76, 29, 29),
                Color.FromRgb(17, 24, 39),
                35);
        }

        if (options.WallpaperName == "Forest Dawn")
        {
            return new LinearGradientBrush(
                Color.FromRgb(20, 83, 45),
                Color.FromRgb(8, 47, 73),
                35);
        }

        return new SolidColorBrush(
            Color.FromRgb(17, 17, 17));
    }

    private void ShowScreensaverMedia(
        VerseDisplayOptions options)
    {
        HideScreensaverMedia();

        if (!string.IsNullOrWhiteSpace(options.ScreensaverPath) &&
            File.Exists(options.ScreensaverPath))
        {
            if (options.ScreensaverMediaKind == "video")
            {
                ScreensaverMediaElement.Source =
                    new Uri(
                        options.ScreensaverPath,
                        UriKind.Absolute);

                ScreensaverMediaElement.Visibility =
                    Visibility.Visible;

                ScreensaverMediaElement.Position =
                    TimeSpan.Zero;

                ScreensaverMediaElement.Play();

                return;
            }

            if (options.ScreensaverMediaKind == "gif")
            {
                ShowGifScreensaver(
                    options.ScreensaverPath);

                return;
            }

            ShowImageScreensaver(
                options.ScreensaverPath);

            return;
        }

        DefaultScreensaverGrid.Visibility =
            Visibility.Visible;
    }

    private void ShowImageScreensaver(
        string path)
    {
        var bitmap =
            new BitmapImage();

        bitmap.BeginInit();
        bitmap.CacheOption =
            BitmapCacheOption.OnLoad;
        bitmap.UriSource =
            new Uri(
                path,
                UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        ScreensaverImage.Source =
            bitmap;

        ScreensaverImage.Visibility =
            Visibility.Visible;
    }

    private void ShowGifScreensaver(
        string path)
    {
        using var stream =
            File.OpenRead(path);

        var decoder =
            new GifBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

        _gifFrames =
            decoder.Frames.ToList();

        if (_gifFrames.Count == 0)
        {
            DefaultScreensaverGrid.Visibility =
                Visibility.Visible;

            return;
        }

        _gifFrameIndex =
            0;

        ScreensaverImage.Source =
            _gifFrames[0];

        ScreensaverImage.Visibility =
            Visibility.Visible;

        if (_gifFrames.Count > 1)
        {
            _gifTimer.Start();
        }
    }

    private void GifTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (_gifFrames.Count == 0)
        {
            _gifTimer.Stop();

            return;
        }

        _gifFrameIndex =
            (_gifFrameIndex + 1) %
            _gifFrames.Count;

        ScreensaverImage.Source =
            _gifFrames[_gifFrameIndex];
    }

    private void HideScreensaverMedia()
    {
        _gifTimer.Stop();

        _gifFrames =
            [];

        ScreensaverImage.Source =
            null;

        ScreensaverImage.Visibility =
            Visibility.Collapsed;

        ScreensaverMediaElement.Stop();

        ScreensaverMediaElement.Source =
            null;

        ScreensaverMediaElement.Visibility =
            Visibility.Collapsed;

        DefaultScreensaverGrid.Visibility =
            Visibility.Collapsed;
    }
}
