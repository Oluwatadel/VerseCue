namespace Versecue.Application.Bible
{
    public sealed record BibleReference(
    string BookName,
    int ChapterNumber,
    int VerseStart,
    int? VerseEnd = null)
    {
        public int EffectiveVerseEnd =>
            VerseEnd ?? VerseStart;
    }
}