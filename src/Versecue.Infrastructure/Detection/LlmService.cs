using System.Text.RegularExpressions;
using Versecue.Application.Dtos;
using Versecue.Application.Interfaces;

namespace Versecue.Infrastructure.Llm;

public sealed class LlmService : ILlmService
{
    private bool _initialized;

    public Task InitializeAsync(string modelPath, CancellationToken ct = default)
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    public Task<LlmResolutionResult> ResolveReferenceAsync(string rawText, string contextText, CancellationToken ct = default)
    {
        if (!_initialized)
        {
            return Task.FromResult(new LlmResolutionResult(false, null, null, null, null, 0.0, "Service not initialized"));
        }

        try
        {
            // Simple rule-based natural language parsing engine simulating local LLM resolution
            var text = rawText.ToLowerInvariant().Trim();

            // Match "the first chapter of genesis"
            var match1 = Regex.Match(text, @"the\s+(first|second|third|fourth|fifth)\s+chapter\s+of\s+([a-z]+)");
            if (match1.Success)
            {
                var numWord = match1.Groups[1].Value;
                var book = match1.Groups[2].Value;
                int ch = ParseOrdinal(numWord);
                return Task.FromResult(new LlmResolutionResult(true, book, ch, null, null, 0.95, null));
            }

            // Match "genesis chapter one"
            var match2 = Regex.Match(text, @"([a-z]+)\s+chapter\s+(one|two|three|four|five|six|seven|eight|nine|ten)");
            if (match2.Success)
            {
                var book = match2.Groups[1].Value;
                var numWord = match2.Groups[2].Value;
                int ch = ParseCardinal(numWord);
                return Task.FromResult(new LlmResolutionResult(true, book, ch, null, null, 0.95, null));
            }

            // General fallback
            return Task.FromResult(new LlmResolutionResult(false, null, null, null, null, 0.0, "Could not resolve reference phrasing"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new LlmResolutionResult(false, null, null, null, null, 0.0, ex.Message));
        }
    }

    private int ParseOrdinal(string word) => word switch
    {
        "first" => 1,
        "second" => 2,
        "third" => 3,
        "fourth" => 4,
        "fifth" => 5,
        _ => 1
    };

    private int ParseCardinal(string word) => word switch
    {
        "one" => 1,
        "two" => 2,
        "three" => 3,
        "four" => 4,
        "five" => 5,
        "six" => 6,
        "seven" => 7,
        "eight" => 8,
        "nine" => 9,
        "ten" => 10,
        _ => 1
    };
}
