namespace RouterQuack.Core.Processors;

/// <summary>
/// Enable iBGP on routers in an iBGP AS, or just using eBGP.
/// </summary>
public class ToggleIbgp(
    ILogger<ToggleIbgp> logger,
    Context context) : IProcessor
{
    public string BeginMessage => "Toggling iBGP for configured routers";
    public ILogger Logger { get; } = logger;
    public Context Context { get; } = context;

    public void Process()
    {
        foreach (var router in Context.Asses.SelectMany(a => a.Routers))
        {
            var isCoreMember = router.ParentAs.Core.HasFlag(CoreType.iBGP);
            var hasBgpInterfaces = router.BorderRouter && router.Interfaces
                .Any(i => i.Bgp != BgpRelationship.None);

            if (isCoreMember || hasBgpInterfaces)
                router.Bgp.Ibgp = true;
        }
    }
}