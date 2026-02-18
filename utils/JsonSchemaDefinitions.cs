using Json.Schema;

namespace IslandMQ.Utils;

public static class JsonSchemaDefinitions
{
    // 基础请求Schema
    public static readonly JsonSchema BaseRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(0))
        )
        .Build();

    // Ping请求Schema
    public static readonly JsonSchema PingRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(0)),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("ping"))
        )
        .Build();

    // Notice请求Schema
    public static readonly JsonSchema NoticeRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command", "args")
        .Properties(
            ("version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(0)),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("notice")),
            ("args", new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                .MinItems(1)
            )
        )
        .Build();

    // Time请求Schema
    public static readonly JsonSchema TimeRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(0)),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("time"))
        )
        .Build();

    // GetLesson请求Schema
    public static readonly JsonSchema GetLessonRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Maximum(0)),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("get_lesson"))
        )
        .Build();

    private static readonly System.Collections.Generic.Dictionary<string, JsonSchema?> SchemaDictionary = new()
    {
        { "ping", PingRequestSchema },
        { "notice", NoticeRequestSchema },
        { "time", TimeRequestSchema },
        { "get_lesson", GetLessonRequestSchema }
        // 注意：添加新命令后，需要在这里添加对应的 schema 映射
    };

    /// <summary>
    /// 根据命令获取相应的Schema
    /// </summary>
    /// <param name="command">命令名称</param>
    /// <returns>对应的Schema，如果命令未知则返回null</returns>
    public static JsonSchema? GetSchemaForCommand(string? command)
    {
        if (command == null)
        {
            return null;
        }
        
        SchemaDictionary.TryGetValue(command, out var schema);
        return schema;
    }
}