using Versecue.Application.Bible;

namespace Versecue.Application.Interfaces;

public interface IVerseDetectionService
{
    Task<BibleReference?> DetectReferenceAsync(
        string transcript,
        CancellationToken cancellationToken = default);
}