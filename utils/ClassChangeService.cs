using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared.Models.Profile;

namespace IslandMQ.Utils;

/// <summary>
/// 换课服务，提供课程替换、交换、批量替换和清除换课功能
/// </summary>
public class ClassChangeService
{
    private readonly IProfileService _profileService;
    private readonly ILessonsService _lessonsService;

    public ClassChangeService(IProfileService profileService, ILessonsService lessonsService)
    {
        _profileService = profileService;
        _lessonsService = lessonsService;
    }

    /// <summary>
    /// 替换单节课程
    /// </summary>
    public void ReplaceClass(DateTime date, int classIndex, Guid newSubjectId)
    {
        // 获取指定日期的课表
        var classPlan = _lessonsService.GetClassPlanByDate(date, out var classPlanId);
        if (classPlan == null || classPlanId == null)
        {
            throw new Exception("未找到指定日期的课表");
        }

        // 获取或创建临时层
        var targetClassPlan = GetOrCreateTempClassPlan(classPlanId.Value);
        if (targetClassPlan == null)
        {
            throw new Exception("获取临时层失败");
        }

        // 检查索引有效性
        if (classIndex < 0 || classIndex >= targetClassPlan.Classes.Count)
        {
            throw new Exception("课程索引无效");
        }

        // 替换课程
        targetClassPlan.Classes[classIndex].SubjectId = newSubjectId;

        // 标记为换课（用于界面高亮）
        targetClassPlan.Classes[classIndex].IsChangedClass = true;

        // 保存
        _profileService.SaveProfile();
    }

    /// <summary>
    /// 交换两节课程
    /// </summary>
    public void SwapClasses(DateTime date, int classIndex1, int classIndex2)
    {
        var classPlan = _lessonsService.GetClassPlanByDate(date, out var classPlanId);
        if (classPlan == null || classPlanId == null)
        {
            throw new Exception("未找到指定日期的课表");
        }

        var targetClassPlan = GetOrCreateTempClassPlan(classPlanId.Value);
        if (targetClassPlan == null)
        {
            throw new Exception("获取临时层失败");
        }

        if (classIndex1 < 0 || classIndex1 >= targetClassPlan.Classes.Count ||
            classIndex2 < 0 || classIndex2 >= targetClassPlan.Classes.Count)
        {
            throw new Exception("课程索引无效");
        }

        // 交换课程
        var temp = targetClassPlan.Classes[classIndex1].SubjectId;
        targetClassPlan.Classes[classIndex1].SubjectId = targetClassPlan.Classes[classIndex2].SubjectId;
        targetClassPlan.Classes[classIndex2].SubjectId = temp;

        // 标记换课状态
        targetClassPlan.Classes[classIndex1].IsChangedClass = true;
        targetClassPlan.Classes[classIndex2].IsChangedClass = true;

        _profileService.SaveProfile();
    }

    /// <summary>
    /// 批量替换课程
    /// </summary>
    public void BatchReplaceClasses(DateTime date, Dictionary<int, Guid> changes)
    {
        var classPlan = _lessonsService.GetClassPlanByDate(date, out var classPlanId);
        if (classPlan == null || classPlanId == null)
        {
            throw new Exception("未找到指定日期的课表");
        }

        var targetClassPlan = GetOrCreateTempClassPlan(classPlanId.Value);
        if (targetClassPlan == null)
        {
            throw new Exception("获取临时层失败");
        }

        foreach (var change in changes)
        {
            if (change.Key >= 0 && change.Key < targetClassPlan.Classes.Count)
            {
                targetClassPlan.Classes[change.Key].SubjectId = change.Value;
                targetClassPlan.Classes[change.Key].IsChangedClass = true;
            }
        }

        _profileService.SaveProfile();
    }

    /// <summary>
    /// 清除换课（恢复原始课表）
    /// </summary>
    public void ClearClassChanges()
    {
        _profileService.ClearTempClassPlan();
        _profileService.SaveProfile();
    }

    /// <summary>
    /// 获取或创建临时层课表
    /// </summary>
    private ClassPlan? GetOrCreateTempClassPlan(Guid originalClassPlanId)
    {
        // 检查是否已有临时层
        var existingOverlay = _profileService.Profile.ClassPlans.Values
            .FirstOrDefault(cp => cp.OverlaySourceId == originalClassPlanId && cp.IsOverlay);

        if (existingOverlay != null)
        {
            return existingOverlay;
        }

        // 创建新的临时层
        var tempClassPlanId = _profileService.CreateTempClassPlan(originalClassPlanId);
        if (tempClassPlanId == null)
        {
            return null;
        }

        return _profileService.Profile.ClassPlans[tempClassPlanId.Value];
    }

    /// <summary>
    /// 获取所有科目（用于选择新科目）
    /// </summary>
    public System.Collections.Generic.Dictionary<Guid, Subject> GetAllSubjects()
    {
        return new System.Collections.Generic.Dictionary<Guid, Subject>(_profileService.Profile.Subjects);
    }

    /// <summary>
    /// 获取指定日期的课程列表
    /// </summary>
    public List<ClassInfo>? GetClasses(DateTime date)
    {
        var classPlan = _lessonsService.GetClassPlanByDate(date, out _);
        return classPlan?.Classes.ToList();
    }
}