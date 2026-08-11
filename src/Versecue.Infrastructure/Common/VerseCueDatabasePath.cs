namespace Versecue.Infrastructure.Common
{
    public static class VerseCueDatabasePath
    {
        public static string GetDatabaseDirectory()
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "VerseCue");

            Directory.CreateDirectory(directory);

            return directory;
        }

        public static string GetDatabasePath()
        {
            return Path.Combine(
                GetDatabaseDirectory(),
                "versecue.db");
        }
    }
}