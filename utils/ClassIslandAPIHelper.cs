using System.Globalization;
using System.Text.Json;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
using IslandMQ.Services.NotificationProviders;
using Microsoft.Extensions.Logging;

namespace IslandMQ.Utils
{
    /// <summary>
    /// ClassIsland API 助手类，提供各种 API 功能
    /// </summary>
    public static class ClassIslandAPIHelper
    {
        private static readonly ILogger? _logger = IAppHost.GetService<ILogger<Plugin>>();

        public static event EventHandler<NotificationEventArgs>? NotificationRequested;

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
            if (parsedData.TryGetProperty("command", out JsonElement commandElement))
            {
                string command = commandElement.ValueKind == JsonValueKind.String
                    ? commandElement.GetString() ?? string.Empty
                    : string.Empty;

                // 根据command调用相应的函数
                return command switch
                {
                    "ping" => Ping(),
                    "notice" => Notice(parsedData),
                    "time" => Time(),
                    "get_lesson" => GetLesson(),
                    "change_lesson" => ChangeLesson(parsedData),
                    "get_classplan" => GetClassPlanByDate(parsedData),
                    // 可以在这里添加更多命令
                    // 注意：添加新命令后，需要在 JsonSchemaDefinitions.cs 文件中添加对应的 schema 定义和映射
                    _ => BuildErrorResult(404, "Command not found"),// 命令不存在，返回404
                };
            }
            else
            {
                // 没有command字段，返回400
                return BuildErrorResult(400, "Missing or invalid 'command' parameter");
            }
        }

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
                DateTime systemTime = DateTime.Now;

                IExactTimeService exactTimeService = IAppHost.GetService<IExactTimeService>();
                if (exactTimeService == null)
                {
                    return BuildErrorResult(500, "Internal server error retrieving time difference");
                }

                DateTime exactTime = exactTimeService.GetCurrentLocalDateTime();
                TimeSpan timeDifference = exactTime - systemTime;

                return new ApiHelperResult
                {
                    StatusCode = 200,
                    Message = timeDifference.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)
                };
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsFatal(ex))
                {
                    throw;
                }
                _logger?.LogError(ex, "Error getting time difference");
                return BuildErrorResult(500, "Internal server error retrieving time difference");
            }
        }

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
            if (parsedData.TryGetProperty("args", out JsonElement argsElement))
            {
                if (argsElement.ValueKind == JsonValueKind.Array)
                {
                    JsonElement.ArrayEnumerator argsArray = argsElement.EnumerateArray();
                    foreach (JsonElement arg in argsArray)
                    {
                        if (arg.ValueKind == JsonValueKind.String)
                        {
                            string argStr = arg.GetString()!;
                            if (argStr.StartsWith("--context="))
                            {
                                // 解析 --context 参数
                                string[] parts = argStr.Split('=', 2);
                                if (parts.Length == 2)
                                {
                                    context = parts[1];
                                }
                            }
                            else if (argStr.StartsWith("--allow-break="))
                            {
                                // 解析 --allow-break 参数
                                string[] parts = argStr.Split('=', 2);
                                if (parts.Length == 2)
                                {
                                    if (bool.TryParse(parts[1], out bool parsed))
                                    {
                                        allowBreak = parsed;
                                    }
                                }
                            }
                            else if (argStr.StartsWith("--mask-duration="))
                            {
                                // 解析 --mask-duration 参数
                                string[] parts = argStr.Split('=', 2);
                                if (parts.Length == 2 && double.TryParse(parts[1], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double duration) && duration >= 0)
                                {
                                    maskDuration = duration;
                                }
                            }
                            else if (argStr.StartsWith("--overlay-duration="))
                            {
                                // 解析 --overlay-duration 参数
                                string[] parts = argStr.Split('=', 2);
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

                return allowBreak
                    ? new ApiHelperResult
                    {
                        StatusCode = 200,
                        Message = "Notice sent successfully"
                    }
                    : new ApiHelperResult
                    {
                        StatusCode = 202,
                        Message = "Notice sent successfully"
                    };
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsFatal(ex))
                {
                    throw;
                }
                _logger?.LogError(ex, "Failed to send notice");
                return BuildErrorResult(503, "Failed to send notice");
            }
        }

        /// <summary>
        /// 获取当前课表及其运行时状态并将这些信息封装为 API 响应。
        /// </summary>
        /// <returns>ApiHelperResult：成功时（状态码 200）其 Data 字段包含课表相关的聚合对象（当前科目、下节课科目、当前状态、时间布局项、课表信息、剩余时间等）；失败时返回包含错误消息和相应状态码（通常为 500）的结果。</returns>
        public static ApiHelperResult GetLesson()
        {
            try
            {
                ILessonsService lessonsService = IAppHost.GetService<ILessonsService>();
                if (lessonsService == null)
                {
                    return BuildErrorResult(500, "Internal server error retrieving lessons service");
                }

                IProfileService profileService = IAppHost.GetService<IProfileService>();
                ClassIsland.Shared.Models.Profile.ClassPlan? currentClassPlan = lessonsService.CurrentClassPlan;
                List<object> enhancedClasses = BuildEnhancedClasses(currentClassPlan?.Classes, profileService);

                // 创建包含所有需要属性的对象
                var lessonData = new
                {
                    lessonsService.CurrentSubject,
                    lessonsService.NextClassSubject,
                    lessonsService.CurrentState,
                    lessonsService.CurrentTimeLayoutItem,
                    CurrentClassPlan = currentClassPlan != null ? new
                    {
                        currentClassPlan.TimeLayoutId,
                        currentClassPlan.TimeRule,
                        Classes = enhancedClasses,
                        currentClassPlan.Name,
                        currentClassPlan.IsOverlay,
                        currentClassPlan.OverlaySourceId,
                        currentClassPlan.OverlaySetupTime,
                        currentClassPlan.IsEnabled,
                        currentClassPlan.AssociatedGroup,
                        currentClassPlan.AttachedObjects,
                        currentClassPlan.IsActive
                    } : null,
                    lessonsService.NextBreakingTimeLayoutItem,
                    lessonsService.NextClassTimeLayoutItem,
                    lessonsService.CurrentSelectedIndex,
                    lessonsService.OnClassLeftTime,
                    lessonsService.OnBreakingTimeLeftTime,
                    lessonsService.IsClassPlanEnabled,
                    lessonsService.IsClassPlanLoaded,
                    lessonsService.IsLessonConfirmed
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
                if (ExceptionHelper.IsFatal(ex))
                {
                    throw;
                }
                _logger?.LogError(ex, "Error getting lesson data");
                return BuildErrorResult(500, "Internal server error retrieving lesson data");
            }
        }

        /// <summary>
        /// 处理换课命令，支持替换、交换、批量替换和清除换课操作
        /// </summary>
        /// <param name="parsedData">包含命令参数的 JSON 对象</param>
        /// <returns>ApiHelperResult：成功时返回状态码 200，失败时返回错误信息</returns>
        public static ApiHelperResult ChangeLesson(JsonElement parsedData)
        {
            try
            {
                IProfileService profileService = IAppHost.GetService<IProfileService>();
                ILessonsService lessonsService = IAppHost.GetService<ILessonsService>();
                if (profileService == null || lessonsService == null)
                {
                    return BuildErrorResult(500, "Internal server error retrieving required services");
                }

                ClassChangeService classChangeService = new(profileService, lessonsService);

                // 解析操作类型
                string operation = string.Empty;
                if (parsedData.TryGetProperty("operation", out JsonElement operationElement))
                {
                    operation = operationElement.ValueKind == JsonValueKind.String
                        ? operationElement.GetString() ?? string.Empty
                        : string.Empty;
                }

                if (string.IsNullOrEmpty(operation))
                {
                    return BuildErrorResult(400, "Missing required parameter 'operation'");
                }

                // 解析日期参数
                DateTime date = DateTime.Today;
                if (parsedData.TryGetProperty("date", out JsonElement dateElement))
                {
                    if (dateElement.ValueKind != JsonValueKind.String)
                    {
                        return BuildErrorResult(400, "Invalid date format: expected string");
                    }
                    string dateStr = dateElement.GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(dateStr) && !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    {
                        return BuildErrorResult(400, "Invalid date format");
                    }
                }

                switch (operation)
                {
                    case "replace":
                        // 解析替换参数
                        if (!parsedData.TryGetProperty("class_index", out JsonElement classIndexElement) ||
                            !parsedData.TryGetProperty("subject_id", out JsonElement subjectIdElement))
                        {
                            return BuildErrorResult(400, "Missing required parameters for replace operation: class_index and subject_id");
                        }

                        if (!classIndexElement.TryGetInt32(out int classIndex))
                        {
                            return BuildErrorResult(400, "Invalid class_index format");
                        }
                        if (subjectIdElement.ValueKind != JsonValueKind.String)
                        {
                            return BuildErrorResult(400, "Invalid subject_id format");
                        }
                        string subjectIdStr = subjectIdElement.GetString() ?? string.Empty;
                        if (!Guid.TryParse(subjectIdStr, out Guid subjectId))
                        {
                            return BuildErrorResult(400, "Invalid subject_id format");
                        }

                        classChangeService.ReplaceClass(date, classIndex, subjectId);
                        return new ApiHelperResult
                        {
                            StatusCode = 200,
                            Message = "Class replaced successfully"
                        };

                    case "swap":
                        // 解析交换参数
                        if (!parsedData.TryGetProperty("class_index1", out JsonElement classIndex1Element) ||
                            !parsedData.TryGetProperty("class_index2", out JsonElement classIndex2Element))
                        {
                            return BuildErrorResult(400, "Missing required parameters for swap operation: class_index1 and class_index2");
                        }

                        if (!classIndex1Element.TryGetInt32(out int classIndex1))
                        {
                            return BuildErrorResult(400, "Invalid class_index1 format");
                        }
                        if (!classIndex2Element.TryGetInt32(out int classIndex2))
                        {
                            return BuildErrorResult(400, "Invalid class_index2 format");
                        }

                        classChangeService.SwapClasses(date, classIndex1, classIndex2);
                        return new ApiHelperResult
                        {
                            StatusCode = 200,
                            Message = "Classes swapped successfully"
                        };

                    case "batch":
                        // 解析批量替换参数
                        if (!parsedData.TryGetProperty("changes", out JsonElement changesElement))
                        {
                            return BuildErrorResult(400, "Missing required parameter for batch operation: changes");
                        }

                        if (changesElement.ValueKind != JsonValueKind.Object)
                        {
                            return BuildErrorResult(400, "Invalid changes format: expected object");
                        }

                        Dictionary<int, Guid> changes = [];
                        List<string> invalidEntries = [];
                        foreach (JsonProperty property in changesElement.EnumerateObject())
                        {
                            if (int.TryParse(property.Name, out int index) &&
                                property.Value.ValueKind == JsonValueKind.String &&
                                Guid.TryParse(property.Value.GetString(), out Guid newSubjectId))
                            {
                                changes[index] = newSubjectId;
                            }
                            else
                            {
                                string errorReason = "";
                                if (!int.TryParse(property.Name, out _))
                                {
                                    errorReason = "invalid index format";
                                }
                                else if (property.Value.ValueKind != JsonValueKind.String)
                                {
                                    errorReason = "invalid subject ID format (expected string)";
                                }
                                else if (!Guid.TryParse(property.Value.GetString(), out _))
                                {
                                    errorReason = "invalid subject ID format (expected GUID)";
                                }
                                invalidEntries.Add($"{property.Name}: {errorReason}");
                            }
                        }

                        if (invalidEntries.Count > 0)
                        {
                            _logger?.LogWarning("Invalid entries found in batch changes: {InvalidEntries}", string.Join(", ", invalidEntries));
                        }

                        classChangeService.BatchReplaceClasses(date, changes);
                        string message = $"Batch replace completed, {changes.Count} classes changed";
                        if (invalidEntries.Count > 0)
                        {
                            message += $"; {invalidEntries.Count} invalid entries were ignored: {string.Join(", ", invalidEntries.Take(5))}{(invalidEntries.Count > 5 ? "..." : "")}";
                        }
                        return new ApiHelperResult
                        {
                            StatusCode = 200,
                            Message = message
                        };

                    case "clear":
                        classChangeService.ClearClassChanges(date);
                        return new ApiHelperResult
                        {
                            StatusCode = 200,
                            Message = "Class changes cleared successfully"
                        };

                    default:
                        return BuildErrorResult(400, "Invalid operation. Supported operations: replace, swap, batch, clear");
                }
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsFatal(ex))
                {
                    throw;
                }

                // Check for client/parameter errors
                if (ex is ArgumentException or ArgumentNullException or FormatException or IndexOutOfRangeException or JsonException)
                {
                    _logger?.LogWarning(ex, "Invalid request parameters for change_lesson command");
                    return BuildErrorResult(400, "Invalid request parameters");
                }

                // For other errors, return 500
                _logger?.LogError(ex, "Error processing change_lesson command");
                return BuildErrorResult(500, "An unexpected error occurred");
            }
        }

        /// <summary>
        /// 构建增强的课程列表，包含科目详情
        /// </summary>
        /// <param name="classes">课程列表</param>
        /// <param name="profileService">配置服务</param>
        /// <returns>增强的课程列表</returns>
        private static List<object> BuildEnhancedClasses(IEnumerable<ClassIsland.Shared.Models.Profile.ClassInfo>? classes, IProfileService? profileService)
        {
            List<object> enhancedClasses = [];

            if (classes != null && profileService != null)
            {
                foreach (ClassIsland.Shared.Models.Profile.ClassInfo classInfo in classes)
                {
                    object? subjectInfo = null;

                    // 尝试获取Subject信息
                    if (profileService.Profile?.Subjects?.TryGetValue(classInfo.SubjectId, out ClassIsland.Shared.Models.Profile.Subject? subject) == true)
                    {
                        subjectInfo = new
                        {
                            subject.Name,
                            subject.Initial,
                            subject.TeacherName,
                            subject.IsOutDoor
                        };
                    }

                    // 创建包含所有属性的增强类对象，确保结构一致
                    var enhancedClass = new
                    {
                        classInfo.SubjectId,
                        classInfo.IsChangedClass,
                        classInfo.IsEnabled,
                        classInfo.AttachedObjects,
                        classInfo.IsActive,
                        Subject = subjectInfo
                    };

                    enhancedClasses.Add(enhancedClass);
                }
            }

            return enhancedClasses;
        }

        /// <summary>
        /// 根据指定日期获取课表及其运行时状态并将这些信息封装为 API 响应。
        /// </summary>
        /// <param name="parsedData">包含日期参数的 JSON 对象</param>
        /// <returns>ApiHelperResult：成功时（状态码 200）其 Data 字段包含指定日期的课表相关信息；失败时返回包含错误消息和相应状态码的结果。</returns>
        public static ApiHelperResult GetClassPlanByDate(JsonElement parsedData)
        {
            try
            {
                ILessonsService lessonsService = IAppHost.GetService<ILessonsService>();
                IProfileService profileService = IAppHost.GetService<IProfileService>();
                if (lessonsService == null)
                {
                    return BuildErrorResult(500, "Internal server error retrieving lessons service");
                }

                // 解析日期参数
                DateTime date = DateTime.Today;
                if (parsedData.TryGetProperty("date", out JsonElement dateElement))
                {
                    if (dateElement.ValueKind != JsonValueKind.String)
                    {
                        return BuildErrorResult(400, "Invalid date format: expected string");
                    }
                    string dateStr = dateElement.GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(dateStr) && !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    {
                        return BuildErrorResult(400, "Invalid date format");
                    }
                }

                // 获取指定日期的课表
                ClassIsland.Shared.Models.Profile.ClassPlan? classPlan = lessonsService.GetClassPlanByDate(date, out _);
                if (classPlan == null)
                {
                    return BuildErrorResult(404, "Class plan not found for the specified date");
                }

                // 获取时间表信息
                ClassIsland.Shared.Models.Profile.TimeLayout? timeLayout = classPlan.TimeLayout;

                // 构建增强的课程列表（包含科目详情）
                List<object> enhancedClasses = BuildEnhancedClasses(classPlan.Classes, profileService);

                // 获取 TimeType == 0 的时间点索引列表（这些时间点对应课程）
                List<int> classTimeLayoutIndices = timeLayout != null
                    ? [.. timeLayout.Layouts.Select((layout, index) => new { layout.TimeType, Index = index })
                        .Where(x => x.TimeType == 0)
                        .Select(x => x.Index)]
                    : [];

                // 创建包含时间和课程对应关系的结构
                var timeLayoutWithClasses = timeLayout != null ? new
                {
                    timeLayout.Name,
                    Layouts = timeLayout.Layouts.Where(layout => layout.TimeType == 0).Select(layout =>
                    {
                        // 找到该时间点在所有 TimeLayout.Layouts 中的索引
                        int layoutIndex = timeLayout.Layouts.IndexOf(layout);
                        // 找到该时间点在所有 TimeType==0 时间点中的顺序位置
                        int classIndex = classTimeLayoutIndices.IndexOf(layoutIndex);
                        object? classInfo = null;
                        if (classIndex >= 0 && classIndex < enhancedClasses.Count)
                        {
                            classInfo = enhancedClasses[classIndex];
                        }
                        return new
                        {
                            layout.StartTime,
                            layout.EndTime,
                            layout.TimeType,
                            layout.IsHideDefault,
                            layout.DefaultClassId,
                            layout.BreakName,
                            layout.ActionSet,
                            layout.AttachedObjects,
                            layout.IsActive,
                            Class = classInfo
                        };
                    }).ToList()
                } : null;

                // 创建包含所有需要属性的对象
                var classPlanData = new
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    ClassPlan = new
                    {
                        classPlan.TimeLayoutId,
                        classPlan.TimeRule,
                        classPlan.Name,
                        classPlan.IsOverlay,
                        classPlan.OverlaySourceId,
                        classPlan.OverlaySetupTime,
                        classPlan.IsEnabled,
                        classPlan.AssociatedGroup,
                        classPlan.AttachedObjects,
                        classPlan.IsActive,
                        TimeLayout = timeLayoutWithClasses
                    }
                };

                return new ApiHelperResult
                {
                    StatusCode = 200,
                    Message = "Class plan retrieved successfully",
                    Data = classPlanData
                };
            }
            catch (Exception ex)
            {
                if (ExceptionHelper.IsFatal(ex))
                {
                    throw;
                }
                _logger?.LogError(ex, "Error getting class plan by date");
                return BuildErrorResult(500, "Internal server error retrieving class plan");
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
}
