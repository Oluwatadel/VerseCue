using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Versecue.Application.Interfaces;
using Versecue.Application.Models.Bible;

namespace Versecue.Wpf;

public partial class VerseDisplayWindow : Window
{
    private readonly IBibleRepository _bibleRepository;
    private VerseNavigationCursor? _navigationCursor;

    public VerseDisplayWindow(
        IBibleRepository bibleRepository)
    {
        InitializeComponent();

        _bibleRepository = bibleRepository;
    }

    private sealed class VerseDisplayItem
    {
        public BibleVerseListItem Verse { get; init; } = null!;

        public string Reference { get; init; } = string.Empty;
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

                    Reference =
                        $"{nextVerse.BookName} " +
                        $"{nextVerse.ChapterNumber}:" +
                        $"{nextVerse.Verse.VerseNumber}"
                }
            ]);

        return true;
    }
}
