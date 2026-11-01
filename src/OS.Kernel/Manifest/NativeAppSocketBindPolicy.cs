using OS.Pal;

namespace OS.Manifest;
public static class NativeAppSocketBindPolicy
{
    public const bool RequiresManifestListenEndpoint = true;
    public const bool OpensSockets = false;
    public const bool CallsPalSocketBind = false;
    public const bool ExportsPalFunctions = false;
    public const bool StartsKestrel = false;
    public static NativeAppSocketBindPolicyStatus ValidateBindEndpoint(ReadOnlySpan<byte> manifestSource, NativeAppLaunchPlanResult launchPlan, OSHostIpv4Endpoint endpoint, out NativeAppSocketBindPolicyResult result)
    {
        if (!launchPlan.Succeeded)
        {
            result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.LaunchPlanFailed, launchPlan.Status, expectedPort: 0, actualPort: endpoint.PortHostOrder, expectedAddress0: 0, expectedAddress1: 0, expectedAddress2: 0, expectedAddress3: 0, endpoint, wildcard: false);
            return result.Status;
        }

        NativeAppLaunchPlan plan = launchPlan.Plan;
        if (!plan.HasListenEndpoint)
        {
            result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.MissingListenEndpoint, launchPlan.Status, expectedPort: 0, actualPort: endpoint.PortHostOrder, expectedAddress0: 0, expectedAddress1: 0, expectedAddress2: 0, expectedAddress3: 0, endpoint, wildcard: false);
            return result.Status;
        }

        if (OSHostIpv4Endpoint.Validate(endpoint) != OSHostStatus.Ok)
        {
            result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.InvalidBindEndpoint, launchPlan.Status, plan.ListenPort, endpoint.PortHostOrder, expectedAddress0: 0, expectedAddress1: 0, expectedAddress2: 0, expectedAddress3: 0, endpoint, wildcard: false);
            return result.Status;
        }

        if (!plan.TryGetListenHost(manifestSource, out ReadOnlySpan<byte> host) || !TryParseIpv4Host(host, out byte expected0, out byte expected1, out byte expected2, out byte expected3))
        {
            result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.InvalidListenHost, launchPlan.Status, plan.ListenPort, endpoint.PortHostOrder, expectedAddress0: 0, expectedAddress1: 0, expectedAddress2: 0, expectedAddress3: 0, endpoint, wildcard: false);
            return result.Status;
        }

        bool wildcard = expected0 == 0 && expected1 == 0 && expected2 == 0 && expected3 == 0;
        if (endpoint.PortHostOrder != plan.ListenPort)
        {
            result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.PortMismatch, launchPlan.Status, plan.ListenPort, endpoint.PortHostOrder, expected0, expected1, expected2, expected3, endpoint, wildcard);
            return result.Status;
        }

        if (endpoint.Address0 != expected0 || endpoint.Address1 != expected1 || endpoint.Address2 != expected2 || endpoint.Address3 != expected3)
        {
            result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.AddressMismatch, launchPlan.Status, plan.ListenPort, endpoint.PortHostOrder, expected0, expected1, expected2, expected3, endpoint, wildcard);
            return result.Status;
        }

        result = NativeAppSocketBindPolicyResult.Create(NativeAppSocketBindPolicyStatus.Success, launchPlan.Status, plan.ListenPort, endpoint.PortHostOrder, expected0, expected1, expected2, expected3, endpoint, wildcard);
        return result.Status;
    }

    private static bool TryParseIpv4Host(ReadOnlySpan<byte> host, out byte address0, out byte address1, out byte address2, out byte address3)
    {
        Span<byte> parts = stackalloc byte[4];
        int partIndex = 0;
        int value = 0;
        int digits = 0;
        for (int index = 0; index < host.Length; index++)
        {
            byte c = host[index];
            if (c >= (byte)'0' && c <= (byte)'9')
            {
                value = (value * 10) + (c - (byte)'0');
                digits++;
                if (digits > 3 || value > byte.MaxValue)
                {
                    return FailParse(out address0, out address1, out address2, out address3);
                }

                continue;
            }

            if (c != (byte)'.' || digits == 0 || partIndex >= 3)
            {
                return FailParse(out address0, out address1, out address2, out address3);
            }

            parts[partIndex] = (byte)value;
            partIndex++;
            value = 0;
            digits = 0;
        }

        if (digits == 0 || partIndex != 3)
        {
            return FailParse(out address0, out address1, out address2, out address3);
        }

        parts[partIndex] = (byte)value;
        address0 = parts[0];
        address1 = parts[1];
        address2 = parts[2];
        address3 = parts[3];
        return true;
    }

    private static bool FailParse(out byte address0, out byte address1, out byte address2, out byte address3)
    {
        address0 = 0;
        address1 = 0;
        address2 = 0;
        address3 = 0;
        return false;
    }
}
