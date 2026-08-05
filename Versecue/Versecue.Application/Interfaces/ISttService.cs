using System;
using System.Threading;
using System.Threading.Tasks;

namespace Versecue.Application.Interfaces;

public class TranscriptReceivedEventArgs : EventArgs
{
    public string Text { get; }
    public long StartOffsetMs { get; }
    public long EndOffsetMs { get; }

    public TranscriptReceivedEventArgs(string text, long startOffsetMs, long endOffsetMs)
    {
        Text = text;
        StartOffsetMs = startOffsetMs;
        EndOffsetMs = endOffsetMs;
    }
}

public interface ISttService
{
    event EventHandler<TranscriptReceivedEventArgs>? TranscriptReceived;
    event EventHandler<string>? TranscribingError;

    Task InitializeAsync(string modelPath, CancellationToken ct = default);
    Task WriteAudioChunkAsync(byte[] audioData, CancellationToken ct = default);
    Task ResetAsync(CancellationToken ct = default);
}
