using System.Diagnostics.CodeAnalysis;

namespace RouterQuack.IO.Yaml.Models;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class Vrf
{
    public string? RouteDistinguisher { get; init; }
    public ICollection<string>? ImportTargets { get; init; }
    public ICollection<string>? ExportTargets { get; init; }
    public ICollection<VrfAddressFamily>? AddressFamilies { get; init; }
}
