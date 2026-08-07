namespace Versecue.Infrastructure.Common;

internal static class ApplicationPaths
{
    private const string AppName = "VerseCue";

    public static string InstallDirectory =>
        AppContext.BaseDirectory;

    public static string ModelPath =>
        Path.Combine(
            InstallDirectory,
            "ggml-base.bin");

    public static string LocalData =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);

    public static string Logs =>
        Path.Combine(LocalData, "Logs");

    public static string Settings =>
        Path.Combine(LocalData, "Settings");

    public static string Cache =>
        Path.Combine(LocalData, "Cache");

    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(LocalData);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Settings);
        Directory.CreateDirectory(Cache);
    }
}