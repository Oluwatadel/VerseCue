using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Versecue.Application.Services;
using Versecue.Infrastructure;
using Versecue.Infrastructure.Persistence;

namespace Versecue.Wpf;

public partial class App : System.Windows.Application
{
    private IServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var configuration =
                new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: false)
                    .Build();

            var services = new ServiceCollection();

            services.AddInfrastructure(configuration);

            _serviceProvider =
                services.BuildServiceProvider();

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VersecueDbContext>();
            await db.Database.EnsureCreatedAsync();

            var verseCue =
                _serviceProvider
                    .GetRequiredService<VerseCueService>();

            verseCue.VerseCueDetected += (_, args) =>
            {
                System.Diagnostics.Debug.WriteLine(
                    $"VERSE CUE: [{args.Reference}] " +
                    $"from transcript: [{args.Transcript}]");
            };

            await verseCue.StartAsync();

            await Task.Delay(
                TimeSpan.FromSeconds(30));

            await verseCue.StopAsync();

            MessageBox.Show(
                "VerseCue pipeline test completed.",
                "VerseCue",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "VerseCue Startup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}