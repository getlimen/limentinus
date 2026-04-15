using Limentinus.Domain.Node;

namespace Limentinus.Application.Common.Interfaces;

public interface ILimenControlClient
{
    Task<EnrollmentOutcome> EnrollAsync(string key, string hostname, string[] roles, string platform, string version, CancellationToken ct);
    Task RunAsync(NodeIdentity id, CancellationToken ct);
}
