using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared.Models.Profile;

namespace IslandMQ.Utils
{
    /// <summary>
    /// 换课服务，提供课程替换、交换、批量替换和清除换课功能
    /// </summary>
    public class ClassChangeService(IProfileService profileService, ILessonsService lessonsService)
    {
        private readonly IProfileService _profileService = profileService;
        private readonly ILessonsService _lessonsService = lessonsService;

        /// <summary>
        /// 替换单节课程
        /// </summary>
        public void ReplaceClass(DateTime date, int classIndex, Guid newSubjectId)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // 获取指定日期的课表
                ClassPlan? classPlan = _lessonsService.GetClassPlanByDate(date, out Guid? classPlanId);
                if (classPlan == null || classPlanId == null)
                {
                    throw new Exception("未找到指定日期的课表");
                }

                // 获取或创建临时层
                ClassPlan targetClassPlan = GetOrCreateTempClassPlan(classPlanId.Value, date) ?? throw new Exception("获取临时层失败");

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
            });
        }

        /// <summary>
        /// 交换两节课程
        /// </summary>
        public void SwapClasses(DateTime date, int classIndex1, int classIndex2)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ClassPlan? classPlan = _lessonsService.GetClassPlanByDate(date, out Guid? classPlanId);
                if (classPlan == null || classPlanId == null)
                {
                    throw new Exception("未找到指定日期的课表");
                }

                ClassPlan targetClassPlan = GetOrCreateTempClassPlan(classPlanId.Value, date) ?? throw new Exception("获取临时层失败");
                if (classIndex1 < 0 || classIndex1 >= targetClassPlan.Classes.Count ||
                    classIndex2 < 0 || classIndex2 >= targetClassPlan.Classes.Count)
                {
                    throw new Exception("课程索引无效");
                }

                // 交换课程
                (targetClassPlan.Classes[classIndex2].SubjectId, targetClassPlan.Classes[classIndex1].SubjectId) = (targetClassPlan.Classes[classIndex1].SubjectId, targetClassPlan.Classes[classIndex2].SubjectId);

                // 标记换课状态
                targetClassPlan.Classes[classIndex1].IsChangedClass = true;
                targetClassPlan.Classes[classIndex2].IsChangedClass = true;

                _profileService.SaveProfile();
            });
        }

        /// <summary>
        /// 批量替换课程
        /// </summary>
        public void BatchReplaceClasses(DateTime date, Dictionary<int, Guid> changes)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ClassPlan? classPlan = _lessonsService.GetClassPlanByDate(date, out Guid? classPlanId);
                if (classPlan == null || classPlanId == null)
                {
                    throw new Exception("未找到指定日期的课表");
                }

                ClassPlan targetClassPlan = GetOrCreateTempClassPlan(classPlanId.Value, date) ?? throw new Exception("获取临时层失败");
                foreach (KeyValuePair<int, Guid> change in changes)
                {
                    if (change.Key >= 0 && change.Key < targetClassPlan.Classes.Count)
                    {
                        targetClassPlan.Classes[change.Key].SubjectId = change.Value;
                        targetClassPlan.Classes[change.Key].IsChangedClass = true;
                    }
                }

                _profileService.SaveProfile();
            });
        }

        /// <summary>
        /// 清除换课（恢复原始课表）
        /// </summary>
        public void ClearClassChanges(DateTime date)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                // 从OrderedSchedules中移除对应日期的临时层关联
                if (_profileService.Profile.OrderedSchedules.ContainsKey(date))
                {
                    _ = _profileService.Profile.OrderedSchedules.Remove(date);
                    _profileService.SaveProfile();
                }
            });
        }

        /// <summary>
        /// 获取或创建临时层课表
        /// </summary>
        private ClassPlan? GetOrCreateTempClassPlan(Guid originalClassPlanId, DateTime date)
        {
            // 1. 首先检查该日期是否已有临时层
            if (_profileService.Profile.OrderedSchedules.TryGetValue(date, out OrderedSchedule? orderedSchedule))
            {
                if (_profileService.Profile.ClassPlans.TryGetValue(orderedSchedule.ClassPlanId, out ClassPlan? existingOverlay))
                {
                    if (existingOverlay.IsOverlay && existingOverlay.OverlaySourceId == originalClassPlanId)
                    {
                        return existingOverlay;
                    }
                }
            }

            // 2. 没有则创建新的临时层
            Guid? tempClassPlanId = _profileService.CreateTempClassPlan(originalClassPlanId, enableDateTime: date);
            return tempClassPlanId == null ? null : _profileService.Profile.ClassPlans[tempClassPlanId.Value];
        }

        /// <summary>
        /// 获取所有科目（用于选择新科目）
        /// </summary>
        public Dictionary<Guid, Subject> GetAllSubjects()
        {
            return new Dictionary<Guid, Subject>(_profileService.Profile.Subjects);
        }

        /// <summary>
        /// 获取指定日期的课程列表
        /// </summary>
        public List<ClassInfo>? GetClasses(DateTime date)
        {
            ClassPlan? classPlan = _lessonsService.GetClassPlanByDate(date, out _);
            return classPlan?.Classes.ToList();
        }
    }
}
