using Microsoft.EntityFrameworkCore;
using Versecue.Domain.Entities;

namespace Versecue.Infrastructure.Persistence;

public sealed class VersecueDbContext : DbContext
{
    public VersecueDbContext(
        DbContextOptions<VersecueDbContext> options)
        : base(options)
    {
    }

    public DbSet<BibleTranslation> BibleTranslations => Set<BibleTranslation>();
    public DbSet<BibleBook> BibleBooks => Set<BibleBook>();
    public DbSet<BibleChapter> BibleChapters => Set<BibleChapter>();
    public DbSet<BibleVerse> BibleVerses => Set<BibleVerse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BibleTranslation>(entity =>
        {
            entity.ToTable("BibleTranslations");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Language)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.LicenseInfo)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasMany(x => x.Books)
                .WithOne(x => x.Translation)
                .HasForeignKey(x => x.TranslationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BibleBook>(entity =>
        {
            entity.ToTable("BibleBooks");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.AliasesJson)
                .IsRequired();

            entity.Property(x => x.Testament)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.TranslationId,
                x.CanonicalOrder
            })
            .IsUnique();

            entity.HasMany(x => x.Chapters)
                .WithOne(x => x.Book)
                .HasForeignKey(x => x.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BibleChapter>(entity =>
        {
            entity.ToTable("BibleChapters");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.ChapterNumber)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.BookId,
                x.ChapterNumber
            })
            .IsUnique();

            entity.HasMany(x => x.Verses)
                .WithOne(x => x.Chapter)
                .HasForeignKey(x => x.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BibleVerse>(entity =>
        {
            entity.ToTable("BibleVerses");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.VerseNumber)
                .IsRequired();

            entity.Property(x => x.VerseEndNumber);

            entity.Property(x => x.Text)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.ChapterId,
                x.VerseNumber
            })
            .IsUnique();
        });
    }
}