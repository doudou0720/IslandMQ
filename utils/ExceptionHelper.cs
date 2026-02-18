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
    /// <summary>
    /// 判断给定异常是否属于致命异常（即通常会导致进程或线程无法继续运行的异常）。
    /// </summary>
    /// <param name="ex">要检查的异常实例。</param>
    /// <returns>`true` 如果异常是 OutOfMemoryException、StackOverflowException、AccessViolationException 或 ThreadAbortException 之一，`false` 否则。</returns>
    public static bool IsFatal(Exception ex)
    {
        return ex is OutOfMemoryException ||
               ex is AccessViolationException;
    }
}