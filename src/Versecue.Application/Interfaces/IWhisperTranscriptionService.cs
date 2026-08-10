using Versecue.Application.Audio;

namespace Versecue.Application.Interfaces
{
    public interface IWhisperTranscriptionService : IDisposable
    {
        event EventHandler<TranscriptReceivedEventArgs>? TranscriptReceived;

        bool IsTranscribing { get; }

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
