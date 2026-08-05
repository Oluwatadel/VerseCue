using System;
using System.Threading;
using System.Threading.Tasks;
using Versecue.Application.Interfaces;

namespace Versecue.Infrastructure.Stt;

public sealed class SttService : ISttService
{
    public event EventHandler<TranscriptReceivedEventArgs>? TranscriptReceived;
    public event EventHandler<string>? TranscribingError;

    private string _modelPath = "";
    private bool _initialized;
    private long _offsetMs;

    public Task InitializeAsync(string modelPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path required", nameof(modelPath));

        _modelPath = modelPath;
        _initialized = true;
        _offsetMs = 0;
        return Task.CompletedTask;
    }

    public Task WriteAudioChunkAsync(byte[] audioData, CancellationToken ct = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("STT Service not initialized");

        // STT simulation: in production, this would feed samples into Whisper.
        // For testing and demo purposes, we do not auto-transcribe background noise,
        // but we allow manual injection of transcripts from the UI.
        _offsetMs += 100; // Increment offset corresponding to 100ms chunks

        return Task.CompletedTask;
    }

    public Task ResetAsync(CancellationToken ct = default)
    {
        _offsetMs = 0;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper method to simulate spoken words (used by the WPF dashboard for demonstration).
    /// </summary>
    public void InjectSimulatedSpeech(string text)
    {
        if (!_initialized) return;

        var durationMs = text.Length * 50; // simple estimate
        var start = _offsetMs;
        var end = _offsetMs + durationMs;
        _offsetMs = end;

        TranscriptReceived?.Invoke(this, new TranscriptReceivedEventArgs(text, start, end));
    }
}
