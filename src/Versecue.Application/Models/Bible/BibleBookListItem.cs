using Versecue.Domain.Enums;

namespace Versecue.Application.Models.Bible;

public sealed class BibleBookListItem
{
    public Guid Id { get; init; }

    public Guid TranslationId { get; init; }

    public int CanonicalOrder { get; init; }

    public string Name { get; init; } = string.Empty;

    public Testament Testament { get; init; }
}