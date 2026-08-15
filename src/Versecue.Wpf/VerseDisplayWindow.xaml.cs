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
    private VerseNavigationCursor? _nextNavigationCursor;
    private VerseNavigationCursor? _prevNavigationCursor;
    private readonly DispatcherTimer _gifTimer;
    private List<BitmapFrame> _gifFrames = [];
    private int _gifFrameIndex;

    private double _defaultFontSize = 38.0;
    private double _minFontSize = 20.0;
    private double _maxFontSize = 60.0;

    // Dependency Properties for DataTemplate Bindings
    public static readonly DependencyProperty ProjectorFontSizeProperty =
        DependencyProperty.Register(nameof(ProjectorFontSize), typeof(double), typeof(VerseDisplayWindow), new PropertyMetadata(38.0));

    public double ProjectorFontSize
    {
        get => (double)GetValue(ProjectorFontSizeProperty);
        set => SetValue(ProjectorFontSizeProperty, value);
    }

    public static readonly DependencyProperty ProjectorReferenceFontSizeProperty =
        DependencyProperty.Register(nameof(ProjectorReferenceFontSize), typeof(double), typeof(VerseDisplayWindow), new PropertyMetadata(21.0));

    public double ProjectorReferenceFontSize
    {
        get => (double)GetValue(ProjectorReferenceFontSizeProperty);
        set => SetValue(ProjectorReferenceFontSizeProperty, value);
    }

    public static readonly DependencyProperty ProjectorSpacingProperty =
        DependencyProperty.Register(nameof(ProjectorSpacing), typeof(Thickness), typeof(VerseDisplayWindow), new PropertyMetadata(new Thickness(0, 0, 0, 34)));

    public Thickness ProjectorSpacing
    {
        get => (Thickness)GetValue(ProjectorSpacingProperty);
        set => SetValue(ProjectorSpacingProperty, value);
    }

    public static readonly DependencyProperty ProjectorTextColorProperty =
        DependencyProperty.Register(nameof(ProjectorTextColor), typeof(Brush), typeof(VerseDisplayWindow), new PropertyMetadata(Brushes.White));

    public Brush ProjectorTextColor
    {
        get => (Brush)GetValue(ProjectorTextColorProperty);
        set => SetValue(ProjectorTextColorProperty, value);
    }

    public static readonly DependencyProperty ProjectorReferenceColorProperty =
        DependencyProperty.Register(nameof(ProjectorReferenceColor), typeof(Brush), typeof(VerseDisplayWindow), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(170, 170, 170))));

    public Brush ProjectorReferenceColor
    {
        get => (Brush)GetValue(ProjectorReferenceColorProperty);
        set => SetValue(ProjectorReferenceColorProperty, value);
    }

    public static readonly DependencyProperty ProjectorTextAlignmentProperty =
        DependencyProperty.Register(nameof(ProjectorTextAlignment), typeof(TextAlignment), typeof(VerseDisplayWindow), new PropertyMetadata(TextAlignment.Center));

    public TextAlignment ProjectorTextAlignment
    {
        get => (TextAlignment)GetValue(ProjectorTextAlignmentProperty);
        set => SetValue(ProjectorTextAlignmentProperty, value);
    }

    public static readonly DependencyProperty ProjectorDisplayMarginProperty =
        DependencyProperty.Register(nameof(ProjectorDisplayMargin), typeof(Thickness), typeof(VerseDisplayWindow), new PropertyMetadata(new Thickness(50)));

    public Thickness ProjectorDisplayMargin
    {
        get => (Thickness)GetValue(ProjectorDisplayMarginProperty);
        set => SetValue(ProjectorDisplayMarginProperty, value);
    }

    public static readonly DependencyProperty ProjectorReferenceVisibilityProperty =
        DependencyProperty.Register(nameof(ProjectorReferenceVisibility), typeof(Visibility), typeof(VerseDisplayWindow), new PropertyMetadata(Visibility.Visible));

    public Visibility ProjectorReferenceVisibility
    {
        get => (Visibility)GetValue(ProjectorReferenceVisibilityProperty);
        set => SetValue(ProjectorReferenceVisibilityProperty, value);
    }

    public VerseDisplayWindow(
        IBibleRepository bibleRepository)
    {
        InitializeComponent();

        _bibleRepository = bibleRepository;
        SizeChanged += (s, e) => RecalculateFontSize();

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

        // Custom Layout Settings
        public double DefaultFontSize { get; init; } = 38;
        public double MinFontSize { get; init; } = 20;
        public double MaxFontSize { get; init; } = 60;
        public double VerseSpacing { get; init; } = 34;
        public double ReferenceFontSize { get; init; } = 21;
        public bool ReferenceVisibility { get; init; } = true;
        public string TextAlignment { get; init; } = "Center";
        public double DisplayMargin { get; init; } = 50;
        public string ProjectionTextColor { get; init; } = "#FFFFFF";
        public string ReferenceColor { get; init; } = "#AAAAAA";
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

        var firstDisplayedVerse = verses.Take(3).First();

        _prevNavigationCursor =
            new VerseNavigationCursor
            {
                TranslationId = firstDisplayedVerse.TranslationId,
                TranslationCode = firstDisplayedVerse.TranslationCode,
                BookId = firstDisplayedVerse.BookId,
                BookName = firstDisplayedVerse.BookName,
                ChapterNumber = firstDisplayedVerse.ChapterNumber,
                VerseNumber = firstDisplayedVerse.Verse.VerseNumber,
                VerseId = firstDisplayedVerse.Verse.Id
            };

        _nextNavigationCursor =
            new VerseNavigationCursor
            {
                TranslationId = lastDisplayedVerse.TranslationId,
                TranslationCode = lastDisplayedVerse.TranslationCode,
                BookId = lastDisplayedVerse.BookId,
                BookName = lastDisplayedVerse.BookName,
                ChapterNumber = lastDisplayedVerse.ChapterNumber,
                VerseNumber = lastDisplayedVerse.Verse.VerseNumber,
                VerseId = lastDisplayedVerse.Verse.Id
            };

        VerseItemsControl.ItemsSource = displayItems;
        RecalculateFontSize();

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

        // Apply formatting settings
        _defaultFontSize = options.DefaultFontSize;
        _minFontSize = options.MinFontSize;
        _maxFontSize = options.MaxFontSize;

        ProjectorReferenceFontSize = options.ReferenceFontSize;
        ProjectorSpacing = new Thickness(0, 0, 0, options.VerseSpacing);
        ProjectorDisplayMargin = new Thickness(options.DisplayMargin);
        ProjectorReferenceVisibility = options.ReferenceVisibility ? Visibility.Visible : Visibility.Collapsed;

        if (Enum.TryParse<TextAlignment>(options.TextAlignment, out var align))
        {
            ProjectorTextAlignment = align;
        }

        try
        {
            var brushConverter = new BrushConverter();
            if (brushConverter.ConvertFromString(options.ProjectionTextColor) is Brush textBrush)
            {
                ProjectorTextColor = textBrush;
            }
            if (brushConverter.ConvertFromString(options.ReferenceColor) is Brush refBrush)
            {
                ProjectorReferenceColor = refBrush;
            }
        }
        catch
        {
            ProjectorTextColor = Brushes.White;
            ProjectorReferenceColor = new SolidColorBrush(Color.FromRgb(170, 170, 170));
        }

        RecalculateFontSize();
    }

    public void ShowScreensaver(
        VerseDisplayOptions options)
    {
        VerseItemsControl.ItemsSource =
            null;

        _prevNavigationCursor = null;
        _nextNavigationCursor = null;

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
        if (_nextNavigationCursor is null)
        {
            return false;
        }

        var nextVerse =
            await _bibleRepository.GetNextVerseAsync(
                _nextNavigationCursor.TranslationId,
                _nextNavigationCursor.VerseId,
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

    public async Task<bool> DisplayPreviousVerseAsync(
        CancellationToken cancellationToken = default)
    {
        if (_prevNavigationCursor is null)
        {
            return false;
        }

        var prevVerse =
            await _bibleRepository.GetPreviousVerseAsync(
                _prevNavigationCursor.TranslationId,
                _prevNavigationCursor.VerseId,
                cancellationToken);

        if (prevVerse is null)
        {
            return false;
        }

        ShowVerses(
            [
                new VerseDisplayRequest
                {
                    TranslationId =
                        prevVerse.TranslationId,

                    TranslationCode =
                        prevVerse.TranslationCode,

                    BookId =
                        prevVerse.BookId,

                    BookName =
                        prevVerse.BookName,

                    ChapterNumber =
                        prevVerse.ChapterNumber,

                    Verse =
                        prevVerse.Verse,

                    Reference = prevVerse.Verse.VerseEndNumber.HasValue
                        ? $"{prevVerse.BookName} {prevVerse.ChapterNumber}:{prevVerse.Verse.VerseNumber}–{prevVerse.Verse.VerseEndNumber}"
                        : $"{prevVerse.BookName} {prevVerse.ChapterNumber}:{prevVerse.Verse.VerseNumber}"
                }
            ]);

        return true;
    }

    private void RecalculateFontSize()
    {
        if (VerseItemsControl.ItemsSource is not List<VerseDisplayItem> items || items.Count == 0)
        {
            return;
        }

        double width = ActualWidth;
        double height = ActualHeight;

        if (width <= 0) width = Width;
        if (height <= 0) height = Height;

        double targetSize = _defaultFontSize;
        int totalLength = items.Sum(x => x.Verse.Text.Length);
        int count = items.Count;

        // Scale by window height relative to 700px
        double heightFactor = Math.Min(1.0, height / 700.0);
        targetSize *= heightFactor;

        // Scale by number of verses
        if (count == 2) targetSize *= 0.85;
        else if (count >= 3) targetSize *= 0.70;

        // Scale by total text character length
        if (totalLength > 400) targetSize *= 0.65;
        else if (totalLength > 200) targetSize *= 0.8;

        // Clamp
        targetSize = Math.Max(_minFontSize, Math.Min(_maxFontSize, targetSize));
        ProjectorFontSize = targetSize;
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
