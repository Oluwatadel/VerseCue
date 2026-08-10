using System.Windows;
using Versecue.Infrastructure.Audio;
using Versecue.Infrastructure.Stt;

namespace Versecue.Wpf;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Audio configuration
            var audioOptions = new AudioOptions();

            // Microphone capture
            var audioCapture = new NAudioCaptureService(audioOptions);

            // Whisper configuration
            var whisperOptions = new WhisperOptions();

            // Whisper engine
            var whisperEngine = new WhisperEngine(whisperOptions);

            // Transcription service
            var transcription = new WhisperTranscriptionService(
                audioCapture,
                whisperEngine);

            // Receive recognized speech
            transcription.TranscriptReceived += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"TRANSCRIPT: [{args.Text}]");
            };

            // Start microphone + Whisper
            await transcription.StartAsync();

            
            // Capture for 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(12));

            // Stop microphone + transcription
            await transcription.StopAsync();

            MessageBox.Show(
                "Transcription test completed.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Clean up
            transcription.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Whisper Test Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
