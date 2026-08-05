using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Versecue.Application;
using Versecue.Application.Interfaces;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;
using Versecue.Infrastructure;
using Versecue.Infrastructure.Persistence.Ef;
using Versecue.Wpf.Services;
using Versecue.Wpf.ViewModels;

namespace Versecue.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // 1. Build and register Configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        
        var config = builder.Build();
        services.AddSingleton<IConfiguration>(config);

        // 2. Add layer services via extension methods
        services.AddApplication();
        services.AddInfrastructure(config);

        // 3. Register Presentation layer services
        services.AddSingleton<IPresentationService, PresentationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        var provider = services.BuildServiceProvider();

        // 4. Migrate and Seed sample data
        try
        {
            var db = provider.GetRequiredService<VersecueDbContext>();
            db.Database.EnsureCreated();
            SeedReferenceData(db);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database seeding failed: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // 5. Start main window
        var mainWindow = provider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = provider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    private void SeedReferenceData(VersecueDbContext db)
    {
        // Only seed if empty
        if (db.BibleTranslations.Any()) return;

        // Create Translation
        var kjv = new BibleTranslation("KJV", "King James Version", "en", "Public Domain");
        db.BibleTranslations.Add(kjv);
        db.SaveChanges(); // Persist translation to get ID

        // Create Books
        var genesis = new BibleBook(kjv.Id, 1, "Genesis", Testament.Old, new[] { "Gen", "Genesis", "Ge." }, kjv);
        var john = new BibleBook(kjv.Id, 43, "John", Testament.New, new[] { "Jn", "John", "Jhn", "Jo." }, kjv);
        var romans = new BibleBook(kjv.Id, 45, "Romans", Testament.New, new[] { "Rom", "Romans", "Ro." }, kjv);

        db.BibleBooks.AddRange(genesis, john, romans);
        db.SaveChanges(); // Persist books to get IDs

        // Create Chapters
        var genCh1 = new BibleChapter(genesis.Id, 1);
        var johnCh3 = new BibleChapter(john.Id, 3);
        var romCh8 = new BibleChapter(romans.Id, 8);

        db.BibleChapters.AddRange(genCh1, johnCh3, romCh8);
        db.SaveChanges(); // Persist chapters to get IDs

        // Create Verses
        var verses = new List<BibleVerse>
        {
            new BibleVerse(genCh1.Id, 1, "In the beginning God created the heaven and the earth."),
            new BibleVerse(genCh1.Id, 2, "And the earth was without form, and void; and darkness was upon the face of the deep. And the Spirit of God moved upon the face of the waters."),
            new BibleVerse(johnCh3.Id, 16, "For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life."),
            new BibleVerse(romCh8.Id, 28, "And we know that all things work together for good to them that love God, to them who are the called according to his purpose.")
        };

        db.BibleVerses.AddRange(verses);
        db.SaveChanges();
    }
}
