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
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "AddSettingsPageGroup" && m.GetParameters().Length == 4);
        return method != null;
    }

    /// <summary>
    /// 尝试获取 SettingsPageInfo 的 Name 字段
    /// </summary>
    public static bool TryGetSettingsPageInfoNameField([MaybeNullWhen(false)] out FieldInfo field)
    {
        Type settingsPageInfoType = typeof(SettingsPageInfo);
        field = settingsPageInfoType
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(f => f.Name == "Name" && f.FieldType == typeof(string));
        return field != null;
    }

    /// <summary>
    /// 尝试获取 SettingsPageInfo 的 GroupId 属性
    /// </summary>
    public static bool TryGetSettingsPageInfoGroupIdProperty([MaybeNullWhen(false)] out PropertyInfo property)
    {
        Type settingsPageInfoType = typeof(SettingsPageInfo);
        property = settingsPageInfoType
            .GetProperties()
            .FirstOrDefault(p => p.Name == "GroupId" && p.PropertyType == typeof(string));
        return property != null;
    }
}
