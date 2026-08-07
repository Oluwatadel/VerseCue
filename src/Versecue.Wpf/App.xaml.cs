using System.Windows;
using Versecue.Infrastructure.Stt;

namespace Versecue.Wpf;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var options = new WhisperOptions();

            using var engine = new WhisperEngine(options);

            await engine.InitializeAsync();

            MessageBox.Show("Whisper initialized successfully!");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Whisper Initialization Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}