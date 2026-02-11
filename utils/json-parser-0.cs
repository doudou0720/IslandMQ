using System.Text.Json;

namespace IslandMQ.Utils;

public class JsonParser0
{
    public static JsonParseResult Parse(string jsonString)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(jsonString);
            var rootElement = jsonDocument.RootElement.Clone();
            
            // 验证基本结构
            if (!rootElement.TryGetProperty("version", out var versionElement) || !versionElement.TryGetInt32(out var version) || version != 0)
            {
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = "Invalid version for JsonParser0"
                };
            }
            
            // 这里可以添加版本0特定的解析逻辑
            // 例如验证必要的字段，转换数据结构等
            
            return new JsonParseResult
            {
                Success = true,
                ParsedData = rootElement,
                Version = 0
            };
        }
        catch (JsonException ex)
        {
            return new JsonParseResult
            {
                Success = false,
                ErrorMessage = $"Invalid JSON for version 0: {ex.Message}"
            };
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new JsonParseResult
            {
                Success = false,
                ErrorMessage = $"Parsing error for version 0: {ex.Message}"
            };
        }
    }
}
