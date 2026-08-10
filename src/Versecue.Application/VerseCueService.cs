using System.ComponentModel.Design;
using Versecue.Application.Audio;
using Versecue.Application.Bible;
using Versecue.Application.Interfaces;

namespace Versecue.Application.Services;

public sealed class VerseCueService : IDisposable
{
    private readonly IWhisperTranscriptionService _transcription;
    private readonly IVerseDetectionService _verseDetection;
    private readonly ILlmService _llm;
    private readonly IBibleReferenceService _bibleReferenceService;

    public VerseCueService(
        IWhisperTranscriptionService transcription,
        IVerseDetectionService verseDetection,
        ILlmService llm,
        IBibleReferenceService bibleReferenceService)
    {
        _transcription = transcription;
        _verseDetection = verseDetection;
        _llm = llm;
        _bibleReferenceService = bibleReferenceService;
        _transcription.TranscriptReceived += OnTranscriptReceived;
    }

    public event EventHandler<VerseCueDetectedEventArgs>? VerseCueDetected;

    public async Task StartAsync(
    CancellationToken cancellationToken = default)
    {
        // Initialize the LLM before any transcripts can arrive.
        await _llm.InitializeAsync(
            string.Empty,
            cancellationToken);

        // Start microphone + Whisper transcription.
        await _transcription.StartAsync(
            cancellationToken);
    }


    public Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        return _transcription.StopAsync(cancellationToken);
    }

    private async void OnTranscriptReceived(
        object? sender,
        TranscriptReceivedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.Text))
                return;

            /*
             * First try to detect an explicit Bible reference,
             * for example:
             *
             * John 3:16
             * Romans 8:28
             * 1 Corinthians 13:4-7
             */
            var reference =
                await _verseDetection.DetectReferenceAsync(e.Text);

            if (reference is not null)
            {
                var normalizedReference =
                    await _bibleReferenceService.NormalizeAsync(reference);

                if (normalizedReference is null)
                    return;

                VerseCueDetected?.Invoke(
                    this,
                    new VerseCueDetectedEventArgs(
                        e.Text,
                        normalizedReference));

                return;
            }

            /*
             * If no explicit reference was detected,
             * try natural-language resolution through the LLM service.
             *
             * Examples:
             *
             * "Genesis chapter one"
             * "the first chapter of Genesis"
             */
            var resolution =
                await _llm.ResolveReferenceAsync(
                    e.Text,
                    string.Empty);

            if (!resolution.IsSuccess)
                return;

            if (string.IsNullOrWhiteSpace(
                    resolution.BookName))
            {
                return;
            }

            if (resolution.ChapterNumber is null)
                return;

            /*
             * The current LlmService can resolve a chapter without
             * identifying a specific verse.
             *
             * We therefore must NOT call .Value on VerseStart
             * until we know it is actually present.
             */
            if (resolution.VerseStart is null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"LLM resolved chapter only: " +
                    $"{resolution.BookName} " +
                    $"{resolution.ChapterNumber}");

                return;
            }

            var resolvedReference =
                new BibleReference(
                    resolution.BookName,
                    resolution.ChapterNumber.Value,
                    resolution.VerseStart.Value,
                    resolution.VerseEnd);

            VerseCueDetected?.Invoke(
                this,
                new VerseCueDetectedEventArgs(
                    e.Text,
                    resolvedReference));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"VerseCue orchestration error: {ex}");
        }
    }

    public void Dispose()
    {
        _transcription.TranscriptReceived -=
            OnTranscriptReceived;

        _transcription.Dispose();

        GC.SuppressFinalize(this);
    }
}

public sealed class VerseCueDetectedEventArgs : EventArgs
{
    public VerseCueDetectedEventArgs(
        string transcript,
        BibleReference reference)
    {
        Transcript = transcript;
        Reference = reference;
    }

    public string Transcript { get; }

    public BibleReference Reference { get; }
}