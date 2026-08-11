using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using Versecue.Application.Interfaces;
using Versecue.Infrastructure;
using Versecue.Infrastructure.Common;
using Versecue.Infrastructure.Persistence;

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
            // Dependency Injection
            // ---------------------------------------------------------

            var services = new ServiceCollection();

            /*
             * We intentionally do NOT use:
             *
             *     configuration.GetConnectionString("VerseCue")
             *
             * for the SQLite database.
             *
             * The database is application/user data and should live
             * under LocalApplicationData rather than beside the EXE.
             */

            var databasePath =
                VerseCueDatabasePath.GetDatabasePath();

            var connectionString =
                $"Data Source={databasePath}";

            services.AddInfrastructure(
                configuration,
                connectionString);

            services.AddTransient<MainWindow>();

            _serviceProvider =
                services.BuildServiceProvider();

            // ---------------------------------------------------------
            // Create application scope
            // ---------------------------------------------------------

            _serviceScope =
                _serviceProvider.CreateScope();

            // ---------------------------------------------------------
            // Resolve and show MainWindow
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