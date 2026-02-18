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

/// <summary>
/// ClassIsland API 助手类，提供各种 API 功能
/// </summary>
public static class ClassIslandAPIHelper
{
    private static readonly ILogger? _logger = IAppHost.GetService<ILogger<Plugin>>();
    
    public static event EventHandler<NotificationEventArgs>? NotificationRequested;
    
    /// <summary>
    /// 处理API请求的方法
    /// </summary>
    /// <param name="parsedData">解析后的数据</param>
    /// <summary>
    /// 根据传入的已解析 JSON 指令分派并执行相应的处理逻辑。
    /// </summary>
    /// <remarks>
    /// 支持的命令包括 "ping"、"notice"、"time" 和 "get_lesson"；若命令缺失或不受支持，将返回相应的错误结果。
    /// </remarks>
    /// <param name="parsedData">包含至少 "command" 字段的已解析 JSON 元素，以及命令所需的其他参数（例如 "args"）。</param>
    /// <returns>包含 HTTP 风格状态码、描述性消息及可选负载的 ApiHelperResult；错误情况通过相应的状态码和消息反映。</returns>
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
    /// <summary>
    /// 构建一个包含指定状态码和消息的错误响应对象。
    /// </summary>
    /// <param name="statusCode">用于表示错误类型的状态码（例如 HTTP 风格的状态码或应用级错误码）。</param>
    /// <param name="message">描述错误的消息文本，作为响应中返回的说明。</param>
    /// <returns>包含指定状态码和消息的 ApiHelperResult 实例。</returns>
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
    /// <summary>
    /// 检查服务可用性并返回简单的确认响应。
    /// </summary>
    /// <returns>ApiHelperResult，StatusCode 为 200，Message 为 "OK".</returns>
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
    /// <summary>
    /// 计算系统时间与精确时间服务返回的本地当前时间之间的差值（以毫秒为单位）。
    /// </summary>
    /// <remarks>
    /// 若找不到 IExactTimeService 或在获取时间过程中发生异常，方法会返回表示内部服务器错误的结果（状态码 500）。
    /// </remarks>
    /// <returns>
    /// 一个 ApiHelperResult；成功时 StatusCode 为 200，Message 为时间差的毫秒数（以不变文化格式的字符串）；失败时返回 StatusCode 为 500 的错误结果。
    /// </returns>
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
    /// <summary>
    /// 根据传入的 args 参数触发用户通知并返回处理结果。
    /// </summary>
    /// <param name="parsedData">包含命令参数的 JSON 对象，期望包含 "args" 字段（字符串数组）。支持的参数形式：
    /// --context=&lt;message&gt;（通知内容）、--allow-break=&lt;true|false&gt;、--mask-duration=&lt;秒数&gt;、--overlay-duration=&lt;秒数&gt;，以及第一个不以 `--` 开头的项作为标题。</param>
    /// <returns>表示处理结果的 ApiHelperResult。StatusCode 可能为 200（已发送且允许中断）、202（已发送但不允许中断）、400（缺少 title）或 503（发送失败）；Message 包含相应的描述。</returns>
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
                                if (bool.TryParse(parts[1], out var parsed))
                                {
                                    allowBreak = parsed;
                                }
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
        if (string.IsNullOrWhiteSpace(title))
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
    /// <summary>
    /// 获取当前课表及其运行时状态并将这些信息封装为 API 响应。
    /// </summary>
    /// <returns>ApiHelperResult：成功时（状态码 200）其 Data 字段包含课表相关的聚合对象（当前科目、下节课科目、当前状态、时间布局项、课表信息、剩余时间等）；失败时返回包含错误消息和相应状态码（通常为 500）的结果。</returns>
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

/// <summary>
/// API 助手结果类，包含泛型数据
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public record ApiHelperResult<T>
{
    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; init; }
    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>
    /// 数据
    /// </summary>
    public T? Data { get; init; }
}

/// <summary>
/// API 助手结果类，使用 object 类型数据
/// </summary>
public record ApiHelperResult : ApiHelperResult<object>;