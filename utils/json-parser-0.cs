using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Json.Schema;

namespace IslandMQ.Utils;

/// <summary>
/// 版本 0 的 JSON 解析器
/// </summary>
public static class JsonParser0
{
    /// <summary>
    /// 递归获取所有验证错误
    /// </summary>
    /// <param name="results">验证结果</param>
    /// <returns>错误消息列表</returns>
    private static IEnumerable<string> AllErrors(EvaluationResults results)
    {
        if (results.Errors != null)
        {
            foreach (var error in results.Errors)
            {
                yield return $"{error.Key}: {error.Value}";
            }
        }
        if (results.Details != null)
        {
            foreach (var detail in results.Details)
            {
                foreach (var error in AllErrors(detail))
                {
                    yield return error;
                }
            }
        }
    }
    
    /// <summary>
    /// 解析版本 0 的 JSON 元素
    /// </summary>
    /// <param name="rootElement">根 JSON 元素</param>
    /// <returns>解析结果</returns>
    public static JsonParseResult Parse(JsonElement rootElement)
    {
        try
        {
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
                var allErrors = AllErrors(validationResult).ToList();
                string errorMessage = "Validation failed: " + (allErrors.Any() ? string.Join("; ", allErrors) : "Unknown validation error");
                
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
        catch (Exception ex)
        {
            if (ExceptionHelper.IsFatal(ex))
            {
                throw;
            }
            return new JsonParseResult
            {
                Success = false,
                ErrorMessage = $"Parsing error for version 0: {ex.Message}"
            };
        }
    }
}
