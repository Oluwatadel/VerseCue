using Versecue.Application.Bible;

namespace Versecue.Application.Interfaces;

public interface IBibleReferenceService
{
    Task<BibleReference?> NormalizeAsync(
        BibleReference reference,
        CancellationToken cancellationToken = default);
}