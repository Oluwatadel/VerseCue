using System;
using System.Collections.Generic;
using System.Windows;
using Versecue.Application.Models.Bible;

namespace Versecue.Wpf;

public partial class VerseDisplayWindow : Window
{
    public VerseDisplayWindow()
    {
        InitializeComponent();
    }


    // ============================================================
    // SHOW VERSES
    // ============================================================

    public void ShowVerses(
        List<(BibleVerseListItem Verse, string Reference)> verses)
    {
        if (verses == null || verses.Count == 0)
        {
            return;
        }


        // Maximum of three verses.

        if (verses.Count > 3)
        {
            verses =
                verses
                    .GetRange(0, 3);
        }


        // Refresh the displayed collection.

        VerseItemsControl.ItemsSource =
            null;

        VerseItemsControl.ItemsSource =
            verses;


        // Show the window if necessary.

        if (!IsVisible)
        {
            Show();
        }


        Activate();

        Focus();
    }
}