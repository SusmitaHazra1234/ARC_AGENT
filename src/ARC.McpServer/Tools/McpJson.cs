using ARC.Data.Serialization;

namespace ARC.McpServer.Tools;

internal static class McpJson
{
    public static string Serialize<T>(T value) => ArcJson.Serialize(value);

    public static T Deserialize<T>(string json) => ArcJson.Deserialize<T>(json);
}
