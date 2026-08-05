using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Versecue.Domain.ValueObjects;

namespace Versecue.Application.Interfaces;

public class AudioDataAvailableEventArgs : EventArgs
{
    public byte[] Data { get; }

    public AudioDataAvailableEventArgs(byte[] data)
    {
        Data = data;
    }
}

public interface IAudioService
{
    event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;
    event EventHandler<string>? AudioCaptureError;

    Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync(CancellationToken ct = default);
    Task StartCaptureAsync(AudioDevice device, CancellationToken ct = default);
    Task StopCaptureAsync(CancellationToken ct = default);
    void Pause();
    void Resume();
    bool IsCapturing { get; }
}
