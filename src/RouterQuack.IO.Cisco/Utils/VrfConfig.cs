using System.Text;
using RouterQuack.Core.Models;

namespace RouterQuack.IO.Cisco.Utils;

internal static class VrfConfig
{
    internal static void ApplyVrfConfig(StringBuilder builder, Router router)
    {
        if (router.Vrfs.Count == 0)
            return;

        builder.AppendLine(ConfigHeader);

        foreach (var vrf in router.Vrfs)
        {
            builder.AppendLine($"ip vrf {vrf.Name}");
            builder.AppendLine($" rd {vrf.RouteDistinguisher}");

            foreach (var rt in vrf.ImportTargets ?? [])
                builder.AppendLine($" route-target import {rt}");

            foreach (var rt in vrf.ExportTargets ?? [])
                builder.AppendLine($" route-target export {rt}");
            builder.AppendLine("!");
        }
    }

    private const string ConfigHeader = "! ================= VRF ================";
}