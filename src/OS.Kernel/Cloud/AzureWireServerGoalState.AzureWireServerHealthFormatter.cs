using System.Text;

namespace OS.Cloud;

public static class AzureWireServerHealthFormatter
{
    public const string ReadyState = "Ready";
    public static AzureWireServerHealthBodyResult FormatReady(AzureWireServerGoalState goalState)
    {
        if (!goalState.IsValid)
        {
            return AzureWireServerHealthBodyResult.Fail(AzureWireServerHealthBodyStatus.InvalidGoalState);
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
