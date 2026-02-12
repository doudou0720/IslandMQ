using System;
using System.Globalization;
using System.Text.Json;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Shared;
using IslandMQ.Services.NotificationProviders;

namespace IslandMQ.Utils;

public static class ClassIslandAPIHelper
{
    public static event EventHandler<NotificationEventArgs>? NotificationRequested;
    
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
                case "notice":
                    return Notice(parsedData);
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
    
    /// <summary>
    /// notice函数，显示提醒
    /// </summary>
    /// <param name="parsedData">解析后的数据</param>
    /// <returns>处理结果</returns>
    public static ApiHelperResult Notice(JsonElement parsedData)
    {
        // 解析参数
        string title = string.Empty;
        string context = string.Empty;
        bool allowBreak = true;
        double maskDuration = 3.0;
        double overlayDuration = 5.0;
        
        // 解析args字段
        if (parsedData.TryGetProperty("args", out var argsElement))
        {
            if (argsElement.ValueKind == JsonValueKind.Array)
            {
                var argsArray = argsElement.EnumerateArray();
                foreach (var arg in argsArray)
                {
                    if (arg.ValueKind == JsonValueKind.String)
                    {
                        string argStr = arg.GetString()!;
                        if (argStr.StartsWith("--context"))
                        {
                            // 解析 --context 参数
                            var parts = argStr.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                context = parts[1];
                            }
                        }
                        else if (argStr.StartsWith("--allow-break="))
                        {
                            // 解析 --allow-break 参数
                            var parts = argStr.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                allowBreak = parts[1].ToLower() == "true";
                            }
                        }
                        else if (argStr.StartsWith("--mask-duration="))
                        {
                            // 解析 --mask-duration 参数
                            var parts = argStr.Split('=', 2);
                            if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double duration) && duration >= 0)
                            {
                                maskDuration = duration;
                            }
                        }
                        else if (argStr.StartsWith("--overlay-duration="))
                        {
                            // 解析 --overlay-duration 参数
                            var parts = argStr.Split('=', 2);
                            if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double duration) && duration >= 0)
                            {
                                overlayDuration = duration;
                            }
                        }
                        else if (!argStr.StartsWith("--"))
                        {
                            // 第一个非选项参数作为 title
                            if (string.IsNullOrEmpty(title))
                            {
                                title = argStr;
                            }
                        }
                    }
                }
            }
        }
        
        // 验证必填参数
        if (string.IsNullOrEmpty(title))
        {
            return BuildErrorResult(400, "Missing required parameter 'title'");
        }
        
        try
        {
            // 触发通知请求事件
            string message = context;
            // 当正文未指定时，强制覆写正文持续时间为0
            if (string.IsNullOrEmpty(message))
            {
                overlayDuration = 0.0;
            }
            NotificationRequested?.Invoke(null, new NotificationEventArgs(title, message, maskDuration, overlayDuration));
            
            // 根据 allow-break 返回相应的状态码
            if (allowBreak)
            {
                return new ApiHelperResult
                {
                    StatusCode = 200,
                    Message = "Notice sent successfully"
                };
            }
            else
            {
                return new ApiHelperResult
                {
                    StatusCode = 202,
                    Message = "Notice sent successfully"
                };
            }
        }
        catch (Exception ex)
        {
            return BuildErrorResult(503, $"Failed to send notice: {ex.Message}");
        }
    }
}

public record ApiHelperResult<T>
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
}

public record ApiHelperResult : ApiHelperResult<object>;
