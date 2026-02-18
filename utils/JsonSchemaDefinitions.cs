using Json.Schema;

namespace IslandMQ.Utils;

/// <summary>
/// JSON Schema 定义类，用于验证 API 请求
/// </summary>
public static class JsonSchemaDefinitions
{
    /// <summary>
    /// 版本 0 的模式定义
    /// </summary>
    public static readonly JsonSchemaBuilder VersionZeroSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Integer)
        .Minimum(0)
        .Maximum(0);

    /// <summary>
    /// 基础请求Schema
    /// </summary>
    public static readonly JsonSchema BaseRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", VersionZeroSchema)
        )
        .Build();

    /// <summary>
    /// Ping请求Schema
    /// </summary>
    public static readonly JsonSchema PingRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", VersionZeroSchema),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("ping"))
        )
        .Build();

    /// <summary>
    /// Notice请求Schema
    /// </summary>
    public static readonly JsonSchema NoticeRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command", "args")
        .Properties(
            ("version", VersionZeroSchema),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("notice")),
            ("args", new JsonSchemaBuilder()
                .Type(SchemaValueType.Array)
                .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                .MinItems(1)
            )
        )
        .Build();

    /// <summary>
    /// Time请求Schema
    /// </summary>
    public static readonly JsonSchema TimeRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", VersionZeroSchema),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("time"))
        )
        .Build();

    /// <summary>
    /// GetLesson请求Schema
    /// </summary>
    public static readonly JsonSchema GetLessonRequestSchema = new JsonSchemaBuilder()
        .Type(SchemaValueType.Object)
        .Required("version", "command")
        .Properties(
            ("version", VersionZeroSchema),
            ("command", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("get_lesson"))
        )
        .Build();

    /// <summary>
    /// Schema 字典，映射命令到对应的 Schema
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, JsonSchema> SchemaDictionary = new()
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
        
        if (SchemaDictionary.TryGetValue(command, out var schema))
        {
            return schema;
        }
        return null;
    }
}