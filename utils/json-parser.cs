using System.Text.Json;

namespace IslandMQ.Utils;

public class JsonParser
{
    public const int MIN_SUPPORTED_VERSION = 0;
    public const int MAX_SUPPORTED_VERSION = 0;
    
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
                    return JsonParser0.Parse(jsonString);
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
            return new JsonParseResult
            {
                Success = false,
                ErrorMessage = $"Parsing error: {ex.Message}"
            };
        }
    }
}

public class JsonParseResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public JsonElement? ParsedData { get; init; }
    public int? Version { get; init; }
}
