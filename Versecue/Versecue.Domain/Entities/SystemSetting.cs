namespace Versecue.Domain.Entities;

/// <summary>
/// Flexible key/value application settings.
/// Absorbs new configuration needs without schema churn.
/// </summary>
public class SystemSetting
{
    public string Key { get; private set; }
    public string Value { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SystemSetting() { } // EF Core

    public SystemSetting(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key required", nameof(key));

        Key = key;
        Value = value ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateValue(string value)
    {
        Value = value ?? string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    // Known setting keys
    public static class Keys
    {
        public const string ActiveTranslationId = "ActiveTranslationId";
        public const string ActiveAudioDeviceId = "ActiveAudioDeviceId";
        public const string ActiveDisplayDeviceId = "ActiveDisplayDeviceId";
        public const string AiConfidenceThreshold = "AiConfidenceThreshold";
        public const string SttModelPath = "SttModelPath";
        public const string LlmModelPath = "LlmModelPath";
        public const string PresenterFontSize = "PresenterFontSize";
        public const string PresenterTheme = "PresenterTheme";
    }
}