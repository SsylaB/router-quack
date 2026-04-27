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
            var isCoreMember = router.ParentAs.Igp == IgpType.iBGP;
            var isPe = router.BorderRouter && router.Interfaces
                .Any(i => i.Bgp != BgpRelationship.None && i.Vrf is not null);
            var isCe = router.BorderRouter && router.Interfaces
                .All(i => i.Bgp == BgpRelationship.None || i.Vrf is null);

            if ((isCoreMember || isPe) && !isCe)
                router.Bgp.Ibgp = true;
        }
    }
}