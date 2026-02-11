using System.Text.Json;

namespace IslandMQ.Utils;

public static class ClassIslandAPIHelper
{
    /// <summary>
    /// 处理API请求的方法
    /// </summary>
    /// <param name="parsedData">解析后的数据</param>
    /// <returns>处理结果</returns>
    public static ApiHelperResult ProcessRequest(JsonElement parsedData)
    {
        // 解析command字段
        if (parsedData.TryGetProperty("command", out var commandElement))
        {
            string command = commandElement.ValueKind == JsonValueKind.String 
                ? commandElement.GetString() ?? string.Empty 
                : string.Empty;
            
            // 根据command调用相应的函数
            switch (command)
            {
                case "ping":
                    return Ping();
                // 可以在这里添加更多命令
                default:
                    // 命令不存在，返回404
                    return BuildErrorResult(404, "Command not found");
            }
        }
        else
        {
            // 没有command字段，返回400
            return BuildErrorResult(400, "Missing or invalid 'command' parameter");
        }
    }
    
    /// <summary>
    /// 构建错误结果
    /// </summary>
    /// <param name="statusCode">状态码</param>
    /// <param name="message">错误消息</param>
    /// <returns>ApiHelperResult实例</returns>
    private static ApiHelperResult BuildErrorResult(int statusCode, string message)
    {
        return new ApiHelperResult
        {
            StatusCode = statusCode,
            Message = message
        };
    }
    
    /// <summary>
    /// ping函数，直接返回OK
    /// </summary>
    /// <returns>处理结果</returns>
    public static ApiHelperResult Ping()
    {
        // 直接完成，返回200 OK
        return new ApiHelperResult
        {
            StatusCode = 200,
            Message = "OK"
        };
    }
}

public record ApiHelperResult<T>
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
}

public record ApiHelperResult : ApiHelperResult<object>;
