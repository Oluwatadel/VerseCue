using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Versecue.Application.Interfaces;
using Versecue.Application.Interfaces.Repository;
using Versecue.Application.Models.Bible;
using Versecue.Infrastructure.Persistence;

namespace Versecue.Wpf;

public partial class MainWindow : Window
{
    private readonly IBibleImportService _bibleImportService;
    private readonly VersecueDbContext _db;
    private readonly IBibleRepository _bibleRepository;

    private bool _loadingBrowser;

    private VerseDisplayWindow? _verseDisplayWindow;


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
        await RefreshBibleStatusAsync();

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
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            await _bibleImportService.ImportAsync(
                dialog.FileName,
                CancellationToken.None);

            await RefreshBibleStatusAsync();

            await LoadTranslationsAsync();

            MessageBox.Show(
                "Bible import completed successfully.",
                "Bible Import",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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


    // ============================================================
    // DATABASE STATUS
    // ============================================================

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


            TranslationText.Text =
                $"{translation.Name} ({translation.Code})";

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


            TranslationComboBox.ItemsSource =
                translations;

            TranslationComboBox.SelectedIndex =
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


            VerseReferenceText.Text =
                "Select a chapter to view verses";
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

    private async void TranslationComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingBrowser)
        {
            return;
        }


        if (TranslationComboBox.SelectedItem
            is not BibleTranslationListItem translation)
        {
            ResetBookAndChapter();

            return;
        }


        Console.WriteLine(
            $"DAPPER UI: Translation selected: " +
            $"{translation.Name} ({translation.Code}) " +
            $"Id={translation.Id}");


        await LoadBooksAsync(translation);
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


            VerseReferenceText.Text =
                "Select a chapter to view verses";

            return;
        }


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


            VerseReferenceText.Text =
                "Select a chapter to view verses";
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


            VerseReferenceText.Text =
                "Select a chapter to view verses";

            return;
        }


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


            VerseReferenceText.Text =
                $"Verses — {verses.Count}";


            VerseCountText.Text =
                verses.Count.ToString();
        }
        catch (Exception ex)
        {
            VerseComboBox.ItemsSource =
                null;


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


    // ============================================================
    // VERSE SELECTION
    // ============================================================

    private void VerseComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (VerseComboBox.SelectedItems.Count <= 3)
        {
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
            VerseComboBox.SelectedItems
                .OfType<BibleVerseListItem>()
                .Take(3)
                .ToList();


        var displayVerses =
            new List<(BibleVerseListItem Verse, string Reference)>();


        foreach (var verse in selectedVerses)
        {
            var reference =
                $"{book.Name} {chapter.ChapterNumber}:{verse.VerseNumber}";


            displayVerses.Add(
                (verse, reference));
        }


        // Create a new window if there is no
        // current display window.

        if (_verseDisplayWindow == null)
        {
            _verseDisplayWindow =
                new VerseDisplayWindow();

            _verseDisplayWindow.Closed +=
                VerseDisplayWindow_Closed;
        }


        _verseDisplayWindow.ShowVerses(
            displayVerses);
    }


    // ============================================================
    // DISPLAY WINDOW CLOSED
    // ============================================================

    private void VerseDisplayWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _verseDisplayWindow = null;
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


        VerseReferenceText.Text =
            "Select a chapter to view verses";
    }
}