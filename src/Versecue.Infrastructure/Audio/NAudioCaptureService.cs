using NAudio.Wave;
using Versecue.Application.Audio;
using Versecue.Application.Interfaces;

namespace Versecue.Infrastructure.Audio;

public sealed class NAudioCaptureService : IAudioCaptureService
{
    private readonly AudioOptions _options;

    private WaveInEvent? _waveIn;
    private bool _disposed;

    public NAudioCaptureService(AudioOptions options)
    {
        _options = options;
    }

    public event EventHandler<AudioChunkEventArgs>? AudioChunkReceived;

    public bool IsCapturing => _waveIn != null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsCapturing)
            return Task.CompletedTask;

        cancellationToken.ThrowIfCancellationRequested();

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(
                _options.SampleRate,
                _options.BitsPerSample,
                _options.Channels),

            BufferMilliseconds = _options.BufferMilliseconds
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _waveIn.StartRecording();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_waveIn == null)
            return Task.CompletedTask;

        _waveIn.StopRecording();

        return Task.CompletedTask;
    }

    private void OnDataAvailable(
        object? sender,
        WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;

        var audioChunk = new byte[e.BytesRecorded];

        Buffer.BlockCopy(
            e.Buffer,
            0,
            audioChunk,
            0,
            e.BytesRecorded);

        var hasNonZeroAudio = audioChunk.Any(b => b != 0);

        System.Diagnostics.Debug.WriteLine(
            $"Audio chunk: {e.BytesRecorded} bytes, " +
            $"non-zero: {hasNonZeroAudio}");

        AudioChunkReceived?.Invoke(
            this,
            new AudioChunkEventArgs(audioChunk));
    }

    private void OnRecordingStopped(
        object? sender,
        StoppedEventArgs e)
    {
        if (_waveIn == null)
            return;

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;

        _waveIn.Dispose();
        _waveIn = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;

            _waveIn.StopRecording();
            _waveIn.Dispose();

            _waveIn = null;
        }

        GC.SuppressFinalize(this);
    }
}