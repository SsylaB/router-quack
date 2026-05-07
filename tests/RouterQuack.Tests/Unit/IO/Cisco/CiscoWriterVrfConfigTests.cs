using System.Net;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RouterQuack.IO.Cisco;
using RouterQuack.Tests.Unit.TestHelpers;

namespace RouterQuack.Tests.Unit.IO.Cisco;

public class CiscoWriterVrfConfigTests
{
    private readonly ILogger<CiscoWriter> _logger = Substitute.For<ILogger<CiscoWriter>>();

    [Test]
    public async Task WriteFiles_BorderRouterWithVrfs_EmitsVrfDefinitions()
    {
        var config = GenerateVrfConfig();

        await Assert.That(config).Contains("! ================= VRF ================");
        await Assert.That(config).Contains("vrf definition CUSTOMER_A");
        await Assert.That(config).Contains("vrf definition CUSTOMER_B");
    }

    [Test]
    public async Task WriteFiles_NonBorderRouterWithVrfs_DoesNotEmitVrfDefinitions()
    {
        var config = GenerateVrfConfig(borderRouter: false);

        await Assert.That(config).DoesNotContain("vrf definition");
        await Assert.That(config).DoesNotContain("! ================= VRF");
    }

    [Test]
    public async Task WriteFiles_VrfDefinition_ContainsRd()
    {
        var config = GenerateVrfConfig();

        await Assert.That(config).Contains(" rd 111:1");
        await Assert.That(config).Contains(" rd 111:2");
    }

    [Test]
    public async Task WriteFiles_VrfDefinition_Ipv4AddressFamily_ContainsRouteTargets()
    {
        var config = GenerateVrfConfig();

        // Check for IPv4 address-family and route-targets
        await Assert.That(config).Contains(" address-family ipv4");
        await Assert.That(config).Contains(" route-target import 111:100");
        await Assert.That(config).Contains(" route-target export 111:100");
        await Assert.That(config).Contains(" route-target import 111:200");
        await Assert.That(config).Contains(" route-target export 111:200");
    }

    [Test]
    public async Task WriteFiles_VrfDefinition_Ipv6AddressFamily_WhenAsSupportsIpv6()
    {
        var config = GenerateVrfConfig(ipVersion: IpVersion.IPv6 | IpVersion.IPv4);

        // Should have both IPv4 and IPv6 address families
        var ipv4Index = config.IndexOf(" address-family ipv4", StringComparison.Ordinal);
        var ipv6Index = config.IndexOf(" address-family ipv6", StringComparison.Ordinal);

        await Assert.That(ipv4Index).IsGreaterThan(-1);
        await Assert.That(ipv6Index).IsGreaterThan(-1);
    }

    [Test]
    public async Task WriteFiles_VrfDefinition_OnlyIpv4_WhenAsSupportsOnlyIpv4()
    {
        var config = GenerateVrfConfig(ipVersion: IpVersion.IPv4);

        await Assert.That(config).Contains(" address-family ipv4");

        // Extract VRF section and verify no IPv6 AF within it
        // VRF section ends at "! ================= OSPF =================" or next major section
        var vrfSectionStart = config.IndexOf("! ================= VRF ================", StringComparison.Ordinal);
        var nextSectionStart = config.IndexOf("! ================= OSPF ================", StringComparison.Ordinal);

        await Assert.That(vrfSectionStart).IsGreaterThan(-1);
        await Assert.That(nextSectionStart).IsGreaterThan(-1);

        var vrfSection = config[vrfSectionStart..nextSectionStart];
        await Assert.That(vrfSection).DoesNotContain(" address-family ipv6");
    }

    [Test]
    public async Task WriteFiles_RouterWithoutVrfs_DoesNotEmitVrfSection()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"router-quack-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Create router without VRFs
            var localInterface = TestData.CreateInterface(name: "GigabitEthernet1/0");
            var localRouter = TestData.CreateRouter(
                name: "R1",
                id: IPAddress.Parse("1.1.1.1"),
                interfaces: [localInterface],
                useDefaultId: false);

            var localAs = TestData.CreateAs(number: 111, routers: [localRouter]);
            var context = ContextFactory.Create(asses: [localAs]);
            var writer = new CiscoWriter(_logger, context);
            writer.WriteFiles(outputDirectory);

            var configPath = Path.Combine(outputDirectory, "111", "R1.cfg");
            var config = await File.ReadAllTextAsync(configPath);

            await Assert.That(config).DoesNotContain("! ================= VRF");
            await Assert.That(config).DoesNotContain("vrf definition");
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, true);
        }
    }

    private string GenerateVrfConfig(
        bool borderRouter = true,
        IpVersion ipVersion = IpVersion.IPv4)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"router-quack-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            // Create VRFs
            var vrfA = new Vrf
            {
                Name = "CUSTOMER_A",
                RouteDistinguisher = "111:1",
                ImportTargets = ["111:100"],
                ExportTargets = ["111:100"]
            };

            var vrfB = new Vrf
            {
                Name = "CUSTOMER_B",
                RouteDistinguisher = "111:2",
                ImportTargets = ["111:200"],
                ExportTargets = ["111:200"]
            };

            Interface? localInterface;
            if (borderRouter)
            {
                // Create interface with eBGP neighbour for border router
                localInterface = TestData.CreateInterface(
                    name: "GigabitEthernet1/0",
                    bgp: BgpRelationship.Client);

                var remoteInterface = TestData.CreateInterface(
                    name: "GigabitEthernet1/0",
                    bgp: BgpRelationship.Provider);

                localInterface.Neighbour = remoteInterface;
                remoteInterface.Neighbour = localInterface;

                localInterface.Ipv4Address = TestData.CreateAddress("198.51.100.1", 31);
                remoteInterface.Ipv4Address = TestData.CreateAddress("198.51.100.2", 31);
            }
            else
            {
                // No eBGP interface for non-border router
                localInterface = TestData.CreateInterface(name: "GigabitEthernet1/0");
            }

            var localRouter = TestData.CreateRouter(
                name: "R1",
                id: IPAddress.Parse("1.1.1.1"),
                interfaces: [localInterface],
                vrfs: [vrfA, vrfB],
                useDefaultId: false);

            var localAs = TestData.CreateAs(
                number: 111,
                ipVersion: ipVersion,
                routers: [localRouter]);

            if (borderRouter)
            {
                var remoteRouter = TestData.CreateRouter(
                    name: "CE1",
                    id: IPAddress.Parse("2.2.2.2"),
                    external: true,
                    interfaces: [localInterface.Neighbour!],
                    useDefaultId: false);
                var remoteAs = TestData.CreateAs(number: 65100, routers: [remoteRouter]);

                var context = ContextFactory.Create(asses: [localAs, remoteAs]);
                var writer = new CiscoWriter(_logger, context);
                writer.WriteFiles(outputDirectory);
            }
            else
            {
                var context = ContextFactory.Create(asses: [localAs]);
                var writer = new CiscoWriter(_logger, context);
                writer.WriteFiles(outputDirectory);
            }

            var configPath = Path.Combine(outputDirectory, "111", "R1.cfg");
            return File.ReadAllText(configPath);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, true);
        }
    }
}