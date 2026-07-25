using System.Text;

namespace OS.Cloud;

public enum AzureWireServerGoalStateStatus
{
    Success = 0,
    EmptyInput = 1,
    InputTooLarge = 2,
    MissingContainerId = 3,
    MissingInstanceId = 4,
    MissingIncarnation = 5
}

public readonly struct AzureWireServerGoalState
{
    public AzureWireServerGoalState(string containerId, string instanceId, string incarnation)
    {
        ContainerId = containerId;
        InstanceId = instanceId;
        Incarnation = incarnation;
    }

    public string ContainerId { get; }

    public string InstanceId { get; }

    public string Incarnation { get; }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ContainerId) &&
        !string.IsNullOrWhiteSpace(InstanceId) &&
        !string.IsNullOrWhiteSpace(Incarnation);
}

public readonly struct AzureWireServerGoalStateParseResult
{
    private AzureWireServerGoalStateParseResult(
        AzureWireServerGoalStateStatus status,
        AzureWireServerGoalState goalState)
    {
        Status = status;
        GoalState = goalState;
    }

    public AzureWireServerGoalStateStatus Status { get; }

    public AzureWireServerGoalState GoalState { get; }

    public bool Succeeded => Status == AzureWireServerGoalStateStatus.Success;

    public static AzureWireServerGoalStateParseResult Success(AzureWireServerGoalState goalState)
    {
        return new AzureWireServerGoalStateParseResult(
            AzureWireServerGoalStateStatus.Success,
            goalState);
    }

    public static AzureWireServerGoalStateParseResult Fail(AzureWireServerGoalStateStatus status)
    {
        return new AzureWireServerGoalStateParseResult(status, default);
    }
}

public static class AzureWireServerGoalStateParser
{
    public const int MaxGoalStateXmlChars = 16 * 1024;

    public static AzureWireServerGoalStateParseResult Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return AzureWireServerGoalStateParseResult.Fail(
                AzureWireServerGoalStateStatus.EmptyInput);
        }

        if (xml.Length > MaxGoalStateXmlChars)
        {
            return AzureWireServerGoalStateParseResult.Fail(
                AzureWireServerGoalStateStatus.InputTooLarge);
        }

        if (!TryExtractElement(xml, "ContainerId", out string containerId))
        {
            return AzureWireServerGoalStateParseResult.Fail(
                AzureWireServerGoalStateStatus.MissingContainerId);
        }

        if (!TryExtractElement(xml, "InstanceId", out string instanceId))
        {
            return AzureWireServerGoalStateParseResult.Fail(
                AzureWireServerGoalStateStatus.MissingInstanceId);
        }

        if (!TryExtractElement(xml, "Incarnation", out string incarnation))
        {
            return AzureWireServerGoalStateParseResult.Fail(
                AzureWireServerGoalStateStatus.MissingIncarnation);
        }

        return AzureWireServerGoalStateParseResult.Success(
            new AzureWireServerGoalState(containerId, instanceId, incarnation));
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

public enum AzureWireServerHealthBodyStatus
{
    Success = 0,
    InvalidGoalState = 1
}

public readonly struct AzureWireServerHealthBodyResult
{
    private AzureWireServerHealthBodyResult(
        AzureWireServerHealthBodyStatus status,
        string body)
    {
        Status = status;
        Body = body;
    }

    public AzureWireServerHealthBodyStatus Status { get; }

    public string Body { get; }

    public bool Succeeded => Status == AzureWireServerHealthBodyStatus.Success;

    public static AzureWireServerHealthBodyResult Success(string body)
    {
        return new AzureWireServerHealthBodyResult(
            AzureWireServerHealthBodyStatus.Success,
            body);
    }

    public static AzureWireServerHealthBodyResult Fail(AzureWireServerHealthBodyStatus status)
    {
        return new AzureWireServerHealthBodyResult(status, string.Empty);
    }
}

public static class AzureWireServerHealthFormatter
{
    public const string ReadyState = "Ready";

    public static AzureWireServerHealthBodyResult FormatReady(AzureWireServerGoalState goalState)
    {
        if (!goalState.IsValid)
        {
            return AzureWireServerHealthBodyResult.Fail(
                AzureWireServerHealthBodyStatus.InvalidGoalState);
        }

        StringBuilder builder = new StringBuilder(256);
        builder.Append("<Health><GoalStateIncarnation>");
        AppendEscapedXml(builder, goalState.Incarnation);
        builder.Append("</GoalStateIncarnation><Container><ContainerId>");
        AppendEscapedXml(builder, goalState.ContainerId);
        builder.Append("</ContainerId><RoleInstanceList><Role><InstanceId>");
        AppendEscapedXml(builder, goalState.InstanceId);
        builder.Append("</InstanceId><Health><State>");
        builder.Append(ReadyState);
        builder.Append("</State></Health></Role></RoleInstanceList></Container></Health>");

        return AzureWireServerHealthBodyResult.Success(builder.ToString());
    }

    private static void AppendEscapedXml(StringBuilder builder, string value)
    {
        foreach (char c in value)
        {
            switch (c)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                case '\'':
                    builder.Append("&apos;");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
    }
}

public enum AzureWireServerRequestPlanStatus
{
    Success = 0,
    InvalidGoalState = 1
}

public readonly struct AzureWireServerHttpRequestPlan
{
    public AzureWireServerHttpRequestPlan(
        string method,
        string host,
        ushort port,
        string pathAndQuery,
        string contentType,
        string body)
    {
        Method = method;
        Host = host;
        Port = port;
        PathAndQuery = pathAndQuery;
        ContentType = contentType;
        Body = body;
    }

    public string Method { get; }

    public string Host { get; }

    public ushort Port { get; }

    public string PathAndQuery { get; }

    public string ContentType { get; }

    public string Body { get; }

    public bool HasBody => Body.Length != 0;

    public int BodyByteLength => Encoding.UTF8.GetByteCount(Body);
}

public readonly struct AzureWireServerRequestPlanResult
{
    private AzureWireServerRequestPlanResult(
        AzureWireServerRequestPlanStatus status,
        AzureWireServerHttpRequestPlan request)
    {
        Status = status;
        Request = request;
    }

    public AzureWireServerRequestPlanStatus Status { get; }

    public AzureWireServerHttpRequestPlan Request { get; }

    public bool Succeeded => Status == AzureWireServerRequestPlanStatus.Success;

    public static AzureWireServerRequestPlanResult Success(AzureWireServerHttpRequestPlan request)
    {
        return new AzureWireServerRequestPlanResult(
            AzureWireServerRequestPlanStatus.Success,
            request);
    }

    public static AzureWireServerRequestPlanResult Fail(AzureWireServerRequestPlanStatus status)
    {
        return new AzureWireServerRequestPlanResult(status, default);
    }
}

public static class AzureWireServerRequestPlanner
{
    public const string PlatformHost = "168.63.129.16";
    public const ushort PlatformPort = 80;
    public const string GoalStatePath = "/machine?comp=goalstate";
    public const string HealthPath = "/machine?comp=health";
    public const string HealthContentType = "text/xml; charset=utf-8";

    public static AzureWireServerHttpRequestPlan CreateGoalStateRequest()
    {
        return new AzureWireServerHttpRequestPlan(
            "GET",
            PlatformHost,
            PlatformPort,
            GoalStatePath,
            string.Empty,
            string.Empty);
    }

    public static AzureWireServerRequestPlanResult CreateReadyHealthRequest(
        AzureWireServerGoalState goalState)
    {
        AzureWireServerHealthBodyResult body = AzureWireServerHealthFormatter.FormatReady(goalState);
        if (!body.Succeeded)
        {
            return AzureWireServerRequestPlanResult.Fail(
                AzureWireServerRequestPlanStatus.InvalidGoalState);
        }

        return AzureWireServerRequestPlanResult.Success(
            new AzureWireServerHttpRequestPlan(
                "POST",
                PlatformHost,
                PlatformPort,
                HealthPath,
                HealthContentType,
                body.Body));
    }
}
