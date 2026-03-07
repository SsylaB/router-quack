using System.Diagnostics.CodeAnalysis;

namespace RouterQuack.IO.Yaml.Models;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class Interface
{
    public required string Neighbour { get; init; }

    public string? Bgp { get; init; }

    public ICollection<string>? Addresses { get; init; }
}