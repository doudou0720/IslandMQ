---
title: "ClassChangeService"
description: "Reference for the overlay-aware service that replaces, swaps, batches, and clears classes."
---

`ClassChangeService` is the mutation engine behind the `change_lesson` command. It lives in `utils/ClassChangeService.cs` and depends on `IProfileService` plus `ILessonsService`.

## Type

- Fully qualified type: `IslandMQ.Utils.ClassChangeService`
- Namespace import: `using IslandMQ.Utils;`
- Source file: `utils/ClassChangeService.cs`

## Signature

```csharp
public class ClassChangeService(IProfileService profileService, ILessonsService lessonsService)
```

### Constructor

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `profileService` | `IProfileService` | — | Provides access to subjects, class plans, overlays, and save operations. |
| `lessonsService` | `ILessonsService` | — | Resolves active or dated class plans from ClassIsland. |

### Public Methods

```csharp
public void ReplaceClass(DateTime date, int classIndex, Guid newSubjectId);
public void SwapClasses(DateTime date, int classIndex1, int classIndex2);
public void BatchReplaceClasses(DateTime date, Dictionary<int, Guid> changes);
public void ClearClassChanges(DateTime date);
public Dictionary<Guid, Subject> GetAllSubjects();
public List<ClassInfo>? GetClasses(DateTime date);
```

| Method | Return type | Description |
|--------|-------------|-------------|
| `ReplaceClass(DateTime date, int classIndex, Guid newSubjectId)` | `void` | Replaces one class in the overlay for the given date. |
| `SwapClasses(DateTime date, int classIndex1, int classIndex2)` | `void` | Swaps two classes by index in the overlay. |
| `BatchReplaceClasses(DateTime date, Dictionary<int, Guid> changes)` | `void` | Applies multiple replacements after validating all indices and subject IDs. |
| `ClearClassChanges(DateTime date)` | `void` | Removes the overlay mapping for the date. |
| `GetAllSubjects()` | `Dictionary<Guid, Subject>` | Returns the available subjects from the profile. |
| `GetClasses(DateTime date)` | `List<ClassInfo>?` | Returns the classes for a date using `ILessonsService.GetClassPlanByDate`. |

## Example

```csharp
using IslandMQ.Utils;

var service = new ClassChangeService(profileService, lessonsService);
service.ReplaceClass(DateTime.Today, 0, subjectId);
```

## Practical Notes

The service marshals mutations onto the Avalonia UI thread when necessary by using `Dispatcher.UIThread.InvokeAsync(...).Wait()`. That is not an implementation detail you should ignore: it is the reason schedule edits remain safe even when requests originate from the background REQ thread. `GetOrCreateTempClassPlan` is private, but it is the critical behavior to remember because every public mutation method relies on it before saving the profile.
