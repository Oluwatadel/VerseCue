using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Versecue.Application.Models.Bible;

namespace Versecue.Wpf;

public partial class VerseDisplayWindow : Window
{
    public VerseDisplayWindow()
    {
        InitializeComponent();
    }

    private sealed class VerseDisplayItem
    {
        public BibleVerseListItem Verse { get; init; } = null!;

        public string Reference { get; init; } = string.Empty;
    }

    public void ShowVerses(
        List<(BibleVerseListItem Verse, string Reference)> verses)
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

        VerseItemsControl.ItemsSource =
            displayItems;

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Focus();
    }
}