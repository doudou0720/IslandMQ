using System.Text.Json;
using System.Linq;
using Json.Schema;

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
            
            // 获取command字段
            if (!rootElement.TryGetProperty("command", out var commandElement) || commandElement.ValueKind != JsonValueKind.String)
            {
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = "Missing or invalid 'command' parameter"
                };
            }
            
            string command = commandElement.GetString()!;
            
            // 使用JsonSchema验证请求
            var schema = JsonSchemaDefinitions.GetSchemaForCommand(command);
            if (schema == null)
            {
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = $"Unknown command: {command}"
                };
            }
            
            var validationResult = schema.Evaluate(rootElement);
            
            if (!validationResult.IsValid)
            {
                // 构建错误信息
                string errorMessage = "Validation failed: ";
                if (validationResult.Errors != null)
                {
                    errorMessage += string.Join("; ", validationResult.Errors.Select(e => $"{e.Key}: {e.Value}"));
                }
                else
                {
                    errorMessage += "Unknown validation error";
                }
                
                return new JsonParseResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
            
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
