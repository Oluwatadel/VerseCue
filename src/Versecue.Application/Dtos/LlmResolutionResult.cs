namespace Versecue.Application.Dtos
{
    public sealed record LlmResolutionResult(
    bool IsSuccess,
    string? BookName,
    int? ChapterNumber,
    int? VerseStart,
    int? VerseEnd,
    double Confidence,
    string? ErrorMessage);
}
