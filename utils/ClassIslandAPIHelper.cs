using System;
using System.Globalization;
using System.Text.Json;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using IslandMQ.Services.NotificationProviders;
using Microsoft.Extensions.Logging;

namespace IslandMQ.Utils;

public static class ClassIslandAPIHelper
{
    private static readonly ILogger? _logger = IAppHost.GetService<ILogger<Plugin>>();
    
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
            case "time":
                return Time();
            case "get_lesson":
                return GetLesson();
            // 可以在这里添加更多命令
            // 注意：添加新命令后，需要在 JsonSchemaDefinitions.cs 文件中添加对应的 schema 定义和映射
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
    /// time函数，返回精确时间与系统时间的差值
    /// </summary>
    /// <returns>处理结果</returns>
    public static ApiHelperResult Time()
    {
        try
        {
            var systemTime = DateTime.Now;
            
            var exactTimeService = IAppHost.GetService<IExactTimeService>();
            if (exactTimeService == null)
            {
                return BuildErrorResult(500, "Internal server error retrieving time difference");
            }
            
            var exactTime = exactTimeService.GetCurrentLocalDateTime();
            var timeDifference = exactTime - systemTime;
            
            return new ApiHelperResult
            {
                StatusCode = 200,
                Message = timeDifference.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting time difference");
            return BuildErrorResult(500, "Internal server error retrieving time difference");
        }
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
                        if (argStr.StartsWith("--context="))
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
            string message = context;
            if (string.IsNullOrEmpty(message))
            {
                overlayDuration = 0.0;
            }
            NotificationRequested?.Invoke(null, new NotificationEventArgs(title, message, maskDuration, overlayDuration));
            
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
            _logger?.LogError(ex, "Failed to send notice");
            return BuildErrorResult(503, "Failed to send notice");
        }
    }

    /// <summary>
    /// get_lesson函数，返回序列化的LessonsService数据
    /// </summary>
    /// <returns>处理结果</returns>
    public static ApiHelperResult GetLesson()
    {
        try
        {
            var lessonsService = IAppHost.GetService<ILessonsService>();
            if (lessonsService == null)
            {
                return BuildErrorResult(500, "Internal server error retrieving lessons service");
            }

            // 创建包含所有需要属性的对象
            var lessonData = new
            {
                CurrentSubject = lessonsService.CurrentSubject,
                NextClassSubject = lessonsService.NextClassSubject,
                CurrentState = lessonsService.CurrentState,
                CurrentTimeLayoutItem = lessonsService.CurrentTimeLayoutItem,
                CurrentClassPlan = lessonsService.CurrentClassPlan,
                NextBreakingTimeLayoutItem = lessonsService.NextBreakingTimeLayoutItem,
                NextClassTimeLayoutItem = lessonsService.NextClassTimeLayoutItem,
                CurrentSelectedIndex = lessonsService.CurrentSelectedIndex,
                OnClassLeftTime = lessonsService.OnClassLeftTime,
                OnBreakingTimeLeftTime = lessonsService.OnBreakingTimeLeftTime,
                IsClassPlanEnabled = lessonsService.IsClassPlanEnabled,
                IsClassPlanLoaded = lessonsService.IsClassPlanLoaded,
                IsLessonConfirmed = lessonsService.IsLessonConfirmed
            };

            return new ApiHelperResult
            {
                StatusCode = 200,
                Message = "Lesson data retrieved successfully",
                Data = lessonData
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting lesson data");
            return BuildErrorResult(500, "Internal server error retrieving lesson data");
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
