using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using Versecue.Infrastructure;

namespace Versecue.Wpf;

public partial class App : System.Windows.Application
{
    private IServiceProvider? _serviceProvider;
    private IServiceScope? _serviceScope;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // ---------------------------------------------------------
            // Configuration
            // ---------------------------------------------------------

            var configuration =
                new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile(
                        "appsettings.json",
                        optional: false,
                        reloadOnChange: false)
                    .Build();

            // ---------------------------------------------------------
            // Runtime database path
            // ---------------------------------------------------------

            var databasePath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "versecue.db");

            var runtimeConnectionString =
                $"Data Source={databasePath}";

            // ---------------------------------------------------------
            // Override ConnectionStrings:VerseCue
            // ---------------------------------------------------------

            var runtimeConfiguration =
                new ConfigurationBuilder()
                    .AddConfiguration(configuration)
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:VerseCue"] =
                                runtimeConnectionString
                        })
                    .Build();

            // ---------------------------------------------------------
            // Services
            // ---------------------------------------------------------

            var services = new ServiceCollection();

            // IMPORTANT:
            // Register IConfiguration so DapperBibleRepository
            // can receive IConfiguration in its constructor.
            services.AddSingleton<IConfiguration>(
                runtimeConfiguration);

            // Infrastructure
            services.AddInfrastructure(
                runtimeConfiguration);

            // Main window
            services.AddTransient<MainWindow>();

            // ---------------------------------------------------------
            // Build service provider ONCE
            // ---------------------------------------------------------

            _serviceProvider =
                services.BuildServiceProvider();

            // ---------------------------------------------------------
            // Create scope
            // ---------------------------------------------------------

            _serviceScope =
                _serviceProvider.CreateScope();

            // ---------------------------------------------------------
            // Resolve MainWindow
            // ---------------------------------------------------------

            var mainWindow =
                _serviceScope
                    .ServiceProvider
                    .GetRequiredService<MainWindow>();

            MainWindow = mainWindow;

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "VerseCue Startup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceScope?.Dispose();

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}