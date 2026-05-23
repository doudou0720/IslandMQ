using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;

namespace IslandMQ.Utils;

/// <summary>
/// 动态反射工具类，用于在低 PluginSdk 版本上使用高版本功能
/// </summary>
public static class InjectService
{
    /// <summary>
    /// 尝试获取 AddSettingsPageGroup 方法
    /// </summary>
    public static bool TryGetAddSettingsPageGroupMethod([MaybeNullWhen(false)] out MethodInfo method)
    {
        Type settingsWindowRegistryExtensionsType = typeof(SettingsWindowRegistryExtensions);
        method = settingsWindowRegistryExtensionsType
            .GetMethods()
            .FirstOrDefault(method => (method.ToString()?.Contains("AddSettingsPageGroup") ?? false) && method.GetParameters().Length == 4);
        return method != null;
    }

    /// <summary>
    /// 获取 SettingsPageInfo 的 Name 字段
    /// </summary>
    public static FieldInfo GetSettingsPageInfoNameField()
    {
        Type settingsPageInfoType = typeof(SettingsPageInfo);
        FieldInfo? field = settingsPageInfoType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(method => method.ToString()?.Contains("Name") ?? false);
        return field!;
    }

    /// <summary>
    /// 获取 SettingsPageInfo 的 GroupId 属性
    /// </summary>
    public static PropertyInfo GetSettingsPageInfoGroupIdProperty()
    {
        Type settingsPageInfoType = typeof(SettingsPageInfo);
        PropertyInfo? property = settingsPageInfoType
            .GetProperties()
            .FirstOrDefault(method => method.ToString()?.Contains("GroupId") ?? false);
        return property!;
    }
}
