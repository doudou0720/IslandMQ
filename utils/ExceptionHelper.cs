using System;

namespace IslandMQ.Utils;

/// <summary>
/// 异常助手类，提供异常相关的工具方法
/// </summary>
public static class ExceptionHelper
{
    /// <summary>
    /// 检查异常是否为致命异常
    /// </summary>
    /// <param name="ex">要检查的异常</param>
    /// <returns>如果异常是致命的则返回 true，否则返回 false</returns>
    public static bool IsFatal(Exception ex)
    {
        return ex is OutOfMemoryException ||
               ex is StackOverflowException ||
               ex is AccessViolationException ||
               ex is ThreadAbortException;
    }
}
