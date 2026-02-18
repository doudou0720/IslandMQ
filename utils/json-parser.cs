using System.Text.Json;

namespace IslandMQ.Utils;

/// <summary>
/// JSON 解析器类，用于解析和验证 API 请求
/// </summary>
public static class JsonParser
{

    
    /// <summary>
    /// 最小支持的版本号
    /// </summary>
    public const int MIN_SUPPORTED_VERSION = 0;
    /// <summary>
    /// 最大支持的版本号
    /// </summary>
    public const int MAX_SUPPORTED_VERSION = 0;
    
    /// <summary>
    /// 解析 JSON 字符串
    /// </summary>
    /// <param name="jsonString">要解析的 JSON 字符串</param>
    /// <summary>
    /// 解析并验证包含版本字段的 JSON 字符串，并根据版本调度到对应的版本解析器。
    /// </summary>
    /// <param name="jsonString">根对象形式的 JSON 文本，必须包含名为 "version" 的整数属性。</param>
    /// <returns>包含解析结果：若成功则 Success 为 true 并填充 ParsedData 与 Version；失败时 Success 为 false 且 ErrorMessage 给出原因。</returns>
    /// <exception cref="Exception">当遇到致命异常且 ExceptionHelper.IsFatal 返回 true 时会重新抛出该异常。</exception>
    public static JsonParseResult Parse(string jsonString)
    {
        try
        {
            // 首先尝试解析基本结构以获取版本号
            JsonElement rootElement;
            using (var jsonDocument = JsonDocument.Parse(jsonString))
            {
                rootElement = jsonDocument.RootElement.Clone();
            }
            
            if (!rootElement.TryGetProperty("version", out var versionElement))
            {
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = "JSON message missing version field"
                };
            }
            
            if (!versionElement.TryGetInt32(out var version))
            {
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = "Version field must be an integer"
                };
            }
            
            // 验证版本范围
            if (version < MIN_SUPPORTED_VERSION || version > MAX_SUPPORTED_VERSION)
            {
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = $"Unsupported version: {version}. Supported range: {MIN_SUPPORTED_VERSION}-{MAX_SUPPORTED_VERSION}"
                };
            }
            
            // 根据版本号调用相应的解析器
            switch (version)
            {
                case 0:
                    return JsonParser0.Parse(rootElement);
                default:
                    // 此分支目前不可达，因为版本已在上方验证
                    // 保留此分支用于防御性编程，防止未来添加新版本时遗漏
                    return new JsonParseResult
                    {
                        Success = false,
                        ErrorMessage = $"Parser not implemented for version: {version}"
                    };
            }
        }
        catch (JsonException ex)
        {
            return new JsonParseResult
            {
                Success = false,
                ErrorMessage = $"Invalid JSON: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            return new JsonParseResult
            {
                Success = false,
                ErrorMessage = $"Parsing error: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// JSON 解析结果类
/// </summary>
public class JsonParseResult
{
    /// <summary>
    /// 解析是否成功
    /// </summary>
    public bool Success { get; init; }
    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// 解析后的数据
    /// </summary>
    public JsonElement? ParsedData { get; init; }
    /// <summary>
    /// 版本号
    /// </summary>
    public int? Version { get; init; }
}