namespace Versecue.Application.Interfaces.Repository
{
    public interface IBibleImportService
    {
        Task ImportAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
