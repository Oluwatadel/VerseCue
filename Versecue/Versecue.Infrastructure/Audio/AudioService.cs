using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Versecue.Application.Interfaces;
using Versecue.Domain.ValueObjects;

namespace Versecue.Infrastructure.Audio;

public sealed class AudioService : IAudioService
{
    public event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;
    public event EventHandler<string>? AudioCaptureError;

    private readonly List<AudioDevice> _devices = new()
    {
        new AudioDevice("DefaultMic", "Primary System Microphone (WASAPI)"),
        new AudioDevice("VirtualMic", "Virtual Audio Capture Device")
    };

    private bool _isCapturing;
    private bool _isPaused;
    private CancellationTokenSource? _cts;

    public Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AudioDevice>>(_devices);
    }

    public Task StartCaptureAsync(AudioDevice device, CancellationToken ct = default)
    {
        if (_isCapturing) return Task.CompletedTask;

        _isCapturing = true;
        _isPaused = false;
        _cts = new CancellationTokenSource();

        // Start background simulation thread to feed empty audio data (silence PCM) to STT
        var token = _cts.Token;
        Task.Run(async () =>
        {
            try
            {
                var buffer = new byte[1600]; // 100ms of 8kHz 16-bit mono PCM
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(100, token);

                    if (!_isPaused)
                    {
                        AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(buffer));
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AudioCaptureError?.Invoke(this, $"Capture failed: {ex.Message}");
            }
        }, token);

        return Task.CompletedTask;
    }

    public Task StopCaptureAsync(CancellationToken ct = default)
    {
        if (!_isCapturing) return Task.CompletedTask;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isCapturing = false;
        _isPaused = false;

        return Task.CompletedTask;
    }

    public void Pause()
    {
        if (_isCapturing)
        {
            _isPaused = true;
        }
    }

    public void Resume()
    {
        if (_isCapturing)
        {
            _isPaused = false;
        }
    }

    public bool IsCapturing => _isCapturing;
}
