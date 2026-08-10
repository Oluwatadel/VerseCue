using System.Diagnostics;
using System.Threading.Channels;
using Versecue.Application;
using Versecue.Application.Audio;
using Versecue.Application.Interfaces;
using Versecue.Infrastructure.Audio;

namespace Versecue.Infrastructure.Stt;

public sealed class WhisperTranscriptionService
    : IWhisperTranscriptionService
{
    private readonly IAudioCaptureService _audioCapture;
    private readonly WhisperEngine _whisperEngine;

    private bool _isTranscribing;
    private bool _disposed;
    private readonly List<byte> _audioBuffer = new();

    private readonly Channel<byte[]> _audioQueue =
        Channel.CreateUnbounded<byte[]>();

    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;


    public WhisperTranscriptionService(
        IAudioCaptureService audioCapture,
        WhisperEngine whisperEngine)
    {
        _audioCapture = audioCapture;
        _whisperEngine = whisperEngine;
    }

    public event EventHandler<TranscriptReceivedEventArgs>? TranscriptReceived;

    public bool IsTranscribing => _isTranscribing;

    public async Task StartAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isTranscribing)
            return;

        await _whisperEngine.InitializeAsync(cancellationToken);
        _processingCts = CancellationTokenSource.CreateLinkedTokenSource(
             cancellationToken);

        _processingTask = ProcessAudioQueueAsync(
            _processingCts.Token);

        _audioCapture.AudioChunkReceived += OnAudioChunkReceived;

        await _audioCapture.StartAsync(cancellationToken);

        _isTranscribing = true;
    }

    public async Task StopAsync(
    CancellationToken cancellationToken = default)
    {
        if (!_isTranscribing)
            return;

        _audioCapture.AudioChunkReceived -= OnAudioChunkReceived;

        await _audioCapture.StopAsync(cancellationToken);

        _isTranscribing = false;

        Debug.WriteLine("STOP: microphone stopped");

        // Tell the worker that no more audio chunks are coming.
        _audioQueue.Writer.TryComplete();

        Debug.WriteLine("STOP: queue completed");

        // Wait for all complete 10-second chunks already in the queue.
        if (_processingTask is not null)
        {
            try
            {
                await _processingTask;

                Debug.WriteLine("STOP: worker completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"STOP: worker error: {ex}");
            }
        }

        _processingTask = null;

        // ---------------------------------------------------------
        // Flush the remaining partial audio.
        // ---------------------------------------------------------

        Debug.WriteLine(
            $"STOP: about to flush remaining audio. " +
            $"Buffer={_audioBuffer.Count} bytes");

        if (_audioBuffer.Count > 0)
        {
            var remainingAudio = _audioBuffer.ToArray();
            var remainingBytes = remainingAudio.Length;

            _audioBuffer.Clear();

            Debug.WriteLine(
                $"STOP: flushing {remainingBytes} bytes");

            var samples = ConvertPcm16ToFloat(
                remainingAudio);

            try
            {
                await foreach (var segment in
                    _whisperEngine.Processor.ProcessAsync(
                        samples,
                        cancellationToken))
                {
                    var text = segment.Text?.Trim();

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    if (text.Contains(
                        "[BLANK_AUDIO]",
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    Debug.WriteLine(
                        $"STOP FLUSH SEGMENT: [{text}] " +
                        $"Start={segment.Start}, " +
                        $"End={segment.End}");

                    TranscriptReceived?.Invoke(
                        this,
                        new TranscriptReceivedEventArgs(
                            text,
                            segment.Start,
                            segment.End));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"STOP: remaining audio processing error: {ex}");
            }
        }
        else
        {
            Debug.WriteLine(
                "STOP: no remaining audio to flush");
        }

        Debug.WriteLine(
            "STOP: remaining audio processed");
    }


    private void OnAudioChunkReceived(
    object? sender,
    AudioChunkEventArgs e)
    {
        try
        {
            _audioBuffer.AddRange(e.AudioChunk);

            Debug.WriteLine(
                $"Whisper buffer: {_audioBuffer.Count} bytes");

            const int bytesPerTenSeconds = 320_000;

            if (_audioBuffer.Count < bytesPerTenSeconds)
                return;

            var audioData = _audioBuffer
                .Take(bytesPerTenSeconds)
                .ToArray();

            _audioBuffer.RemoveRange(
                0,
                bytesPerTenSeconds);

            // Send the completed 10-second chunk
            // to the Whisper processing queue.
            if (!_audioQueue.Writer.TryWrite(audioData))
            {
                Debug.WriteLine(
                    "Whisper queue is closed; audio chunk discarded.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Audio buffering error: {ex}");
        }
    }




    private async Task ProcessAudioQueueAsync(
    CancellationToken cancellationToken)
    {
        await foreach (var audioData in
            _audioQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var samples = ConvertPcm16ToFloat(audioData);

                await foreach (var segment in
                    _whisperEngine.Processor.ProcessAsync(samples))
                {
                    var text = segment.Text?.Trim();

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    if (text.Contains(
                        "[BLANK_AUDIO]",
                        StringComparison.OrdinalIgnoreCase))
                        continue;

                    System.Diagnostics.Debug.WriteLine(
                        $"WHISPER SEGMENT: [{text}] " +
                        $"Start={segment.Start}, End={segment.End}");

                    TranscriptReceived?.Invoke(
                        this,
                        new TranscriptReceivedEventArgs(
                            text,
                            segment.Start,
                            segment.End));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Whisper processing error: {ex}");
            }
        }
    }

    private async Task ProcessAudioAsync(byte[] audioData)
    {

        var samples = ConvertPcm16ToFloat(audioData);

        await foreach (var segment in
            _whisperEngine.Processor.ProcessAsync(samples))
        {
            var text = segment.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (text.Contains(
                "[BLANK_AUDIO]",
                StringComparison.OrdinalIgnoreCase))
                continue;

            System.Diagnostics.Debug.WriteLine(
                $"WHISPER SEGMENT: [{text}] " +
                $"Start={segment.Start}, End={segment.End}");

            TranscriptReceived?.Invoke(
                this,
                new TranscriptReceivedEventArgs(
                    text,
                    segment.Start,
                    segment.End));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _audioCapture.AudioChunkReceived -= OnAudioChunkReceived;

        _audioCapture.Dispose();
        _whisperEngine.Dispose();

        GC.SuppressFinalize(this);
    }

    private static float[] ConvertPcm16ToFloat(byte[] pcmData)
    {
        var sampleCount = pcmData.Length / 2;
        var samples = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(
                pcmData,
                i * 2);

            samples[i] = sample / 32768f;
        }

        return samples;
    }
}