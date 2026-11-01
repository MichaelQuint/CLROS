using System.Text;

namespace OS.Cloud;
public static class AzureWireServerGoalStateParser
{
    public const int MaxGoalStateXmlChars = 16 * 1024;
    public static AzureWireServerGoalStateParseResult Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return AzureWireServerGoalStateParseResult.Fail(AzureWireServerGoalStateStatus.EmptyInput);
        }

        if (xml.Length > MaxGoalStateXmlChars)
        {
            return AzureWireServerGoalStateParseResult.Fail(AzureWireServerGoalStateStatus.InputTooLarge);
        }

        if (!TryExtractElement(xml, "ContainerId", out string containerId))
        {
            return AzureWireServerGoalStateParseResult.Fail(AzureWireServerGoalStateStatus.MissingContainerId);
        }

        if (!TryExtractElement(xml, "InstanceId", out string instanceId))
        {
            return AzureWireServerGoalStateParseResult.Fail(AzureWireServerGoalStateStatus.MissingInstanceId);
        }

        if (!TryExtractElement(xml, "Incarnation", out string incarnation))
        {
            return AzureWireServerGoalStateParseResult.Fail(AzureWireServerGoalStateStatus.MissingIncarnation);
        }

        return AzureWireServerGoalStateParseResult.Success(new AzureWireServerGoalState(containerId, instanceId, incarnation));
    }

    private static bool TryExtractElement(string xml, string elementName, out string value)
    {
        string openTag = "<" + elementName + ">";
        string closeTag = "</" + elementName + ">";
        int open = xml.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (open < 0)
        {
            value = string.Empty;
            return false;
        }

        int valueStart = open + openTag.Length;
        int close = xml.IndexOf(closeTag, valueStart, StringComparison.OrdinalIgnoreCase);
        if (close < 0 || close < valueStart)
        {
            value = string.Empty;
            return false;
        }

        value = xml[valueStart..close].Trim();
        return value.Length != 0;
    }
}
