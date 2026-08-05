using Microsoft.EntityFrameworkCore;
using Versecue.Domain.Entities;

namespace Versecue.Infrastructure.Persistence.Ef;

/// <summary>
/// EF Core DbContext for mutable data (SermonSession, TranscriptSegment, DetectedReference, Presentation, SystemSetting).
/// Reference data (BibleTranslation, BibleBook, BibleChapter, BibleVerse) is accessed via Dapper, not EF Core.
/// </summary>
public class VersecueDbContext : DbContext
{
    public VersecueDbContext(DbContextOptions<VersecueDbContext> options) : base(options) { }

    public DbSet<SermonSession> SermonSessions => Set<SermonSession>();
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<DetectedReference> DetectedReferences => Set<DetectedReference>();
    public DbSet<Presentation> Presentations => Set<Presentation>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<BibleTranslation> BibleTranslations => Set<BibleTranslation>();
    public DbSet<BibleBook> BibleBooks => Set<BibleBook>();
    public DbSet<BibleChapter> BibleChapters => Set<BibleChapter>();
    public DbSet<BibleVerse> BibleVerses => Set<BibleVerse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SermonSession
        modelBuilder.Entity<SermonSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.State).HasConversion<string>();
            entity.HasIndex(e => e.StartedAt);
        });

        // TranscriptSegment
        modelBuilder.Entity<TranscriptSegment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasOne(e => e.SermonSession)
                .WithMany(s => s.TranscriptSegments)
                .HasForeignKey(e => e.SermonSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.SermonSessionId, e.StartOffsetMs });
        });

        // DetectedReference
        modelBuilder.Entity<DetectedReference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.State).HasConversion<string>();
            entity.Property(e => e.DetectionSource).HasConversion<string>();
            entity.HasOne(e => e.SermonSession)
                .WithMany(s => s.DetectedReferences)
                .HasForeignKey(e => e.SermonSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.TranscriptSegment)
                .WithMany()
                .HasForeignKey(e => e.TranscriptSegmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.SermonSessionId, e.DetectedAt });
        });

        // Presentation
        modelBuilder.Entity<Presentation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.HasOne(e => e.SermonSession)
                .WithOne()
                .HasForeignKey<Presentation>(e => e.SermonSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.DetectedReference)
                .WithMany()
                .HasForeignKey(e => e.DetectedReferenceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SystemSetting
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        // BibleTranslation
        modelBuilder.Entity<BibleTranslation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Language).IsRequired();
            entity.HasMany(e => e.Books)
                .WithOne(b => b.Translation)
                .HasForeignKey(b => b.TranslationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BibleBook
        modelBuilder.Entity<BibleBook>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Testament).HasConversion<string>();
            entity.Property(e => e.AliasesJson).IsRequired();
            entity.HasMany(e => e.Chapters)
                .WithOne(c => c.Book)
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BibleChapter
        modelBuilder.Entity<BibleChapter>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(e => e.Verses)
                .WithOne(v => v.Chapter)
                .HasForeignKey(v => v.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // BibleVerse
        modelBuilder.Entity<BibleVerse>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Text).IsRequired();
        });
    }

}