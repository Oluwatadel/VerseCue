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

        public double DisplayHeight { get; set; }
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

        // Divide the available screen area between the selected verses.
        // This prevents the display from becoming taller when 2 or 3
        // verses are selected.
        var availableHeight = Math.Max(250, ActualHeight - 100);
        var itemHeight = availableHeight / displayItems.Count;

        foreach (var item in displayItems)
        {
            item.DisplayHeight = itemHeight;
        }

        VerseItemsControl.ItemsSource = displayItems;

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Focus();
    }
}
