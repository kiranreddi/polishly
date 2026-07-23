using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Capture;

public interface ICaptureEngine
{
    Task<SelectionContext> CaptureSelectionAsync(CancellationToken ct = default);
}
