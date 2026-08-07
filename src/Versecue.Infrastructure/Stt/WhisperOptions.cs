namespace Versecue.Infrastructure.Stt;

public sealed class WhisperOptions
{
    public string Language { get; init; }
        = WhisperConstants.DefaultLanguage;

    public string ModelPath { get; init; }
        = Common.ApplicationPaths.ModelPath;
}