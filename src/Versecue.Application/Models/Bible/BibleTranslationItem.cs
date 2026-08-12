namespace Versecue.Application.Models.Bible;

public sealed class BibleTranslationListItem
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;
}