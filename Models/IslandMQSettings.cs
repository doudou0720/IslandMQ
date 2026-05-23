using System.Text.Json.Serialization;
using ClassIsland.Shared.Models.Profile;

namespace IslandMQ.Models;

/// <summary>
/// IslandMQ 插件的设置类，用于配置REQ和PUB服务器的各项参数。
/// </summary>
public class IslandMQSettings
{
    /// <summary>
    /// 获取或设置是否启用REQ服务器。
    /// </summary>
    [JsonPropertyName("isReqServerEnabled")]
    public bool IsReqServerEnabled { get; set; } = true;

    /// <summary>
    /// 获取或设置REQ服务器的端口号。
    /// </summary>
    [JsonPropertyName("reqServerPort")]
    public int ReqServerPort { get; set; } = 5555;

    /// <summary>
    /// 获取或设置是否启用PUB服务器。
    /// </summary>
    [JsonPropertyName("isPubServerEnabled")]
    public bool IsPubServerEnabled { get; set; } = true;

    /// <summary>
    /// 获取或设置PUB服务器的端口号。
    /// </summary>
    [JsonPropertyName("pubServerPort")]
    public int PubServerPort { get; set; } = 5556;

    /// <summary>
    /// 获取或设置服务器的IP地址。
    /// </summary>
    [JsonPropertyName("serverIp")]
    public string ServerIp { get; set; } = "127.0.0.1";

    /// <summary>
    /// 获取或设置是否启用 HTTP 服务器。
    /// </summary>
    [JsonPropertyName("isHttpServerEnabled")]
    public bool IsHttpServerEnabled { get; set; } = false;

    /// <summary>
    /// 获取或设置 HTTP 服务器的端口号。
    /// </summary>
    [JsonPropertyName("httpServerPort")]
    public int HttpServerPort { get; set; } = 8080;
}
