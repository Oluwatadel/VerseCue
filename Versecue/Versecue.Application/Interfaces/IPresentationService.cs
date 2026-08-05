using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Versecue.Domain.ValueObjects;

namespace Versecue.Application.Interfaces;

public interface IPresentationService
{
    Task<IReadOnlyList<Display>> GetDisplaysAsync(CancellationToken ct = default);
    Task ShowScriptureAsync(Display display, string text, string referenceDisplay, CancellationToken ct = default);
    Task HideAsync(CancellationToken ct = default);
}
