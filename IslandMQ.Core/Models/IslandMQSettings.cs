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
    /// 获取或设置 NetMQ 服务器的 IP 地址。
    /// </summary>
    [JsonPropertyName("netMqServerIp")]
    public string NetMqServerIp { get; set; } = "127.0.0.1";

    /// <summary>
    /// 获取或设置 HTTP 服务器的 IP 地址。
    /// </summary>
    [JsonPropertyName("httpServerIp")]
    public string HttpServerIp { get; set; } = "127.0.0.1";

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

    /// <summary>
    /// 获取或设置是否启用 CORS（跨域资源共享）。
    /// </summary>
    [JsonPropertyName("isCorsEnabled")]
    public bool IsCorsEnabled { get; set; } = false;

    /// <summary>
    /// 获取或设置允许的 CORS 来源（用逗号分隔，空表示仅允许同源）。
    /// </summary>
    [JsonPropertyName("corsAllowedOrigins")]
    public string CorsAllowedOrigins { get; set; } = "";
}
