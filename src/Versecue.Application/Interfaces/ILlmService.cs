using System.Threading;
using System.Threading.Tasks;
using Versecue.Application.Dtos;

namespace Versecue.Application.Interfaces;

public interface ILlmService
{
    Task InitializeAsync(string modelPath, CancellationToken ct = default);
    Task<LlmResolutionResult> ResolveReferenceAsync(string rawText, string contextText, CancellationToken ct = default);
}
