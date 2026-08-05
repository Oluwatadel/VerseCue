using System.Threading;
using System.Threading.Tasks;

namespace Versecue.Application.Interfaces;

public sealed record LlmResolutionResult(
    bool IsSuccess,
    string? BookName,
    int? ChapterNumber,
    int? VerseStart,
    int? VerseEnd,
    double Confidence,
    string? ErrorMessage);

public interface ILlmService
{
    Task InitializeAsync(string modelPath, CancellationToken ct = default);
    Task<LlmResolutionResult> ResolveReferenceAsync(string rawText, string contextText, CancellationToken ct = default);
}
