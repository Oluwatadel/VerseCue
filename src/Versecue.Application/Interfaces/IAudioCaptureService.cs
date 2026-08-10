using Versecue.Infrastructure.Audio;

namespace Versecue.Application
{
    public interface IAudioCaptureService : IDisposable
    {
        event EventHandler<AudioChunkEventArgs>? AudioChunkReceived;

        bool IsCapturing { get; }

        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
