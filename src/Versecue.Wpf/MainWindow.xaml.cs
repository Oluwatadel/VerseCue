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
    // IMPORT
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
            return;

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

    private async Task LoadBooksAsync(
    BibleTranslationListItem translation)
    {
        try
        {
            _loadingBrowser = true;

            Console.WriteLine(
                $"DAPPER UI: Loading books for {translation.Name} ({translation.Code})");

            var books =
                await _bibleRepository
                    .GetBooksByTranslationAsync(
                        translation.Id);

            Console.WriteLine(
                $"DAPPER UI: Found {books.Count} books.");

            BookComboBox.ItemsSource = books;

            BookComboBox.IsEnabled =
                books.Count > 0;

            ChapterComboBox.ItemsSource = null;
            ChapterComboBox.IsEnabled = false;

            if (books.Count > 0)
            {
                BookComboBox.SelectedIndex = 0;
            }
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

            var translations =
                await _bibleRepository
                    .GetActiveTranslationsAsync();

            Console.WriteLine(
                $"DAPPER UI: Found {translations.Count} translations.");

            TranslationComboBox.ItemsSource = translations;

            BookComboBox.ItemsSource = null;
            ChapterComboBox.ItemsSource = null;

            BookComboBox.IsEnabled = false;
            ChapterComboBox.IsEnabled = false;

            TranslationComboBox.SelectedIndex =
                translations.Count > 0 ? 0 : -1;
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

        // Load the first translation manually.
        if (TranslationComboBox.SelectedItem
            is BibleTranslationListItem translation)
        {
            await LoadBooksAsync(translation);
        }
    }


    // ============================================================
    // TRANSLATION SELECTED
    // ============================================================

    private async void TranslationComboBox_SelectionChanged(
    object sender,
    System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingBrowser)
            return;

        if (TranslationComboBox.SelectedItem
            is not BibleTranslationListItem translation)
        {
            BookComboBox.ItemsSource = null;
            ChapterComboBox.ItemsSource = null;

            BookComboBox.IsEnabled = false;
            ChapterComboBox.IsEnabled = false;

            return;
        }

        Console.WriteLine(
            $"DAPPER UI: Translation selected: " +
            $"{translation.Name} ({translation.Code}) " +
            $"Id={translation.Id}");

        await LoadBooksAsync(translation);
    }


    // ============================================================
    // BOOK SELECTED
    // ============================================================

    private async void BookComboBox_SelectionChanged(
    object sender,
    System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingBrowser)
            return;

        if (BookComboBox.SelectedItem
            is not BibleBookListItem book)
        {
            ChapterComboBox.ItemsSource = null;
            ChapterComboBox.IsEnabled = false;

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

            ChapterComboBox.ItemsSource = chapters;

            ChapterComboBox.IsEnabled =
                chapters.Count > 0;

            if (chapters.Count > 0)
            {
                ChapterComboBox.SelectedIndex = 0;
            }
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
    System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ChapterComboBox.SelectedItem
            is not BibleChapterListItem chapter)
        {
            VerseListView.ItemsSource = null;
            VerseHeaderText.Text = "Select a chapter to view verses";
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;

            var verses =
                await _bibleRepository.GetVersesAsync(
                    chapter.Id,
                    CancellationToken.None);

            VerseListView.ItemsSource = verses;

            VerseHeaderText.Text =
                $"Verses — {verses.Count}";

            VerseCountText.Text =
                verses.Count.ToString();
        }
        catch (Exception ex)
        {
            VerseListView.ItemsSource = null;

            MessageBox.Show(
                ex.ToString(),
                "Bible Browser",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }
}