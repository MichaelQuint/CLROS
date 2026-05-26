using System.Runtime.CompilerServices;
using OS.Cloud;

internal static class AzureWireServerGoalStateTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("cloud.azure.wireserver parses goal state", ref failures, TestParseGoalState);
        Run("cloud.azure.wireserver parses case-insensitive trimmed tags", ref failures, TestParsesCaseInsensitiveTrimmedTags);
        Run("cloud.azure.wireserver rejects empty goal-state elements", ref failures, TestRejectsEmptyElements);
        Run("cloud.azure.wireserver rejects missing close tags", ref failures, TestRejectsMissingCloseTags);
        Run("cloud.azure.wireserver uses first duplicate goal-state tag", ref failures, TestUsesFirstDuplicateTag);
        Run("cloud.azure.wireserver enforces goal-state size boundary", ref failures, TestGoalStateSizeBoundary);
        Run("cloud.azure.wireserver rejects missing ids", ref failures, TestRejectsMissingIds);
        Run("cloud.azure.wireserver formats ready health body", ref failures, TestFormatsReadyHealthBody);
        Run("cloud.azure.wireserver plans goalstate and health requests", ref failures, TestPlansRequests);

        if (failures != 0)
        {
            throw new InvalidOperationException("Azure WireServer host tests failed: " + failures);
        }
    }

    private static void Run(string name, ref int failures, Func<bool> test)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestParseGoalState()
    {
        AzureWireServerGoalStateParseResult result =
            AzureWireServerGoalStateParser.Parse(GoalStateXml("container-123", "instance-456", "7"));

        return result.Succeeded
            && result.GoalState.ContainerId == "container-123"
            && result.GoalState.InstanceId == "instance-456"
            && result.GoalState.Incarnation == "7";
    }

    private static bool TestParsesCaseInsensitiveTrimmedTags()
    {
        AzureWireServerGoalStateParseResult result =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState>" +
                "<incarnation>  7  </INCARNATION>" +
                "<Container><containerid>\tcontainer-123\r\n</CONTAINERID></Container>" +
                "<RoleInstance><INSTANCEID>\n instance-456 \t</instanceid></RoleInstance>" +
                "</GoalState>");

        return result.Succeeded
            && result.GoalState.ContainerId == "container-123"
            && result.GoalState.InstanceId == "instance-456"
            && result.GoalState.Incarnation == "7";
    }

    private static bool TestRejectsEmptyElements()
    {
        AzureWireServerGoalStateParseResult emptyContainer =
            AzureWireServerGoalStateParser.Parse(GoalStateXml("   ", "instance-456", "7"));
        AzureWireServerGoalStateParseResult emptyInstance =
            AzureWireServerGoalStateParser.Parse(GoalStateXml("container-123", "\t\r\n", "7"));
        AzureWireServerGoalStateParseResult emptyIncarnation =
            AzureWireServerGoalStateParser.Parse(GoalStateXml("container-123", "instance-456", string.Empty));

        return emptyContainer.Status == AzureWireServerGoalStateStatus.MissingContainerId
            && emptyInstance.Status == AzureWireServerGoalStateStatus.MissingInstanceId
            && emptyIncarnation.Status == AzureWireServerGoalStateStatus.MissingIncarnation;
    }

    private static bool TestRejectsMissingCloseTags()
    {
        AzureWireServerGoalStateParseResult missingContainerClose =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState><Incarnation>7</Incarnation><Container><ContainerId>container-123</Container><RoleInstance><InstanceId>instance-456</InstanceId></RoleInstance></GoalState>");
        AzureWireServerGoalStateParseResult missingInstanceClose =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState><Incarnation>7</Incarnation><Container><ContainerId>container-123</ContainerId></Container><RoleInstance><InstanceId>instance-456</RoleInstance></GoalState>");
        AzureWireServerGoalStateParseResult missingIncarnationClose =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState><Incarnation>7<Container><ContainerId>container-123</ContainerId></Container><RoleInstance><InstanceId>instance-456</InstanceId></RoleInstance></GoalState>");

        return missingContainerClose.Status == AzureWireServerGoalStateStatus.MissingContainerId
            && missingInstanceClose.Status == AzureWireServerGoalStateStatus.MissingInstanceId
            && missingIncarnationClose.Status == AzureWireServerGoalStateStatus.MissingIncarnation;
    }

    private static bool TestUsesFirstDuplicateTag()
    {
        AzureWireServerGoalStateParseResult result =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState>" +
                "<Incarnation>7</Incarnation><Incarnation>8</Incarnation>" +
                "<Container><ContainerId>container-first</ContainerId><ContainerId>container-second</ContainerId></Container>" +
                "<RoleInstance><InstanceId>instance-first</InstanceId><InstanceId>instance-second</InstanceId></RoleInstance>" +
                "</GoalState>");

        return result.Succeeded
            && result.GoalState.ContainerId == "container-first"
            && result.GoalState.InstanceId == "instance-first"
            && result.GoalState.Incarnation == "7";
    }

    private static bool TestGoalStateSizeBoundary()
    {
        string exactLimitXml = PadGoalStateXmlToMaxChars(GoalStateXml("container-123", "instance-456", "7"));
        AzureWireServerGoalStateParseResult exactLimit =
            AzureWireServerGoalStateParser.Parse(exactLimitXml);
        AzureWireServerGoalStateParseResult oversized =
            AzureWireServerGoalStateParser.Parse(exactLimitXml + "x");

        return exactLimitXml.Length == AzureWireServerGoalStateParser.MaxGoalStateXmlChars
            && exactLimit.Succeeded
            && exactLimit.GoalState.ContainerId == "container-123"
            && exactLimit.GoalState.InstanceId == "instance-456"
            && exactLimit.GoalState.Incarnation == "7"
            && oversized.Status == AzureWireServerGoalStateStatus.InputTooLarge;
    }

    private static bool TestRejectsMissingIds()
    {
        AzureWireServerGoalStateParseResult missingContainer =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState><Incarnation>7</Incarnation><RoleInstance><InstanceId>instance-456</InstanceId></RoleInstance></GoalState>");
        AzureWireServerGoalStateParseResult missingInstance =
            AzureWireServerGoalStateParser.Parse(
                "<GoalState><Incarnation>7</Incarnation><Container><ContainerId>container-123</ContainerId></Container></GoalState>");
        AzureWireServerGoalStateParseResult oversized =
            AzureWireServerGoalStateParser.Parse(new string('x', AzureWireServerGoalStateParser.MaxGoalStateXmlChars + 1));

        return missingContainer.Status == AzureWireServerGoalStateStatus.MissingContainerId
            && missingInstance.Status == AzureWireServerGoalStateStatus.MissingInstanceId
            && oversized.Status == AzureWireServerGoalStateStatus.InputTooLarge;
    }

    private static bool TestFormatsReadyHealthBody()
    {
        AzureWireServerGoalState goalState = new AzureWireServerGoalState(
            "container-123",
            "instance-456",
            "7");

        AzureWireServerHealthBodyResult result =
            AzureWireServerHealthFormatter.FormatReady(goalState);

        return result.Succeeded
            && result.Body.Contains("<GoalStateIncarnation>7</GoalStateIncarnation>", StringComparison.Ordinal)
            && result.Body.Contains("<ContainerId>container-123</ContainerId>", StringComparison.Ordinal)
            && result.Body.Contains("<InstanceId>instance-456</InstanceId>", StringComparison.Ordinal)
            && result.Body.Contains("<State>Ready</State>", StringComparison.Ordinal);
    }

    private static bool TestPlansRequests()
    {
        AzureWireServerHttpRequestPlan goalState =
            AzureWireServerRequestPlanner.CreateGoalStateRequest();
        AzureWireServerRequestPlanResult health =
            AzureWireServerRequestPlanner.CreateReadyHealthRequest(
                new AzureWireServerGoalState("container-123", "instance-456", "7"));

        return goalState.Method == "GET"
            && goalState.Host == AzureWireServerRequestPlanner.PlatformHost
            && goalState.Port == AzureWireServerRequestPlanner.PlatformPort
            && goalState.PathAndQuery == AzureWireServerRequestPlanner.GoalStatePath
            && !goalState.HasBody
            && health.Succeeded
            && health.Request.Method == "POST"
            && health.Request.PathAndQuery == AzureWireServerRequestPlanner.HealthPath
            && health.Request.ContentType == AzureWireServerRequestPlanner.HealthContentType
            && health.Request.BodyByteLength > 0;
    }

    private static string GoalStateXml(string containerId, string instanceId, string incarnation)
    {
        return
            "<GoalState>" +
            "<Incarnation>" + incarnation + "</Incarnation>" +
            "<Container><ContainerId>" + containerId + "</ContainerId></Container>" +
            "<RoleInstance><InstanceId>" + instanceId + "</InstanceId></RoleInstance>" +
            "</GoalState>";
    }

    private static string PadGoalStateXmlToMaxChars(string xml)
    {
        int padding = AzureWireServerGoalStateParser.MaxGoalStateXmlChars - xml.Length;
        if (padding < 0)
        {
            throw new InvalidOperationException("Goal-state fixture exceeds parser maximum.");
        }

        return xml + new string(' ', padding);
    }
}
