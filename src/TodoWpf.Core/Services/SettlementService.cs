using TodoWpf.Core.Models;
using TodoWpf.Core.Utils;

namespace TodoWpf.Core.Services;

/// <summary>结算结果（纯数据，无任何用户通知）。</summary>
public sealed record SettleResult(
    AppData Data,
    int ArchivedCount,
    int MarkedOverdue,
    bool Changed);

/// <summary>
/// 跨天结算（需求文档 §2.9，全程静默）：
/// 已完成页中完成日期 ≠ 今天的事项归档进 Archive[完成日]；未完成待办全部重算逾期标记（顺序不变）；
/// 更新 LastSettleDate；关机跨天的在下次启动调用同一函数补结算；幂等（同一天重复调用零移动）。
/// </summary>
public static class SettlementService
{
    public static SettleResult Settle(AppData data, DateTimeOffset now)
    {
        var todayKey = LocalDate.Key(now);

        // 逐桶复制：追加时只改副本，保证输入数据不被改动（不可变语义）
        var archive = data.Archive.ToDictionary(kv => kv.Key, kv => new List<TodoItem>(kv.Value));
        var remainingCompleted = new List<TodoItem>(data.Completed.Count);
        var archivedCount = 0;

        foreach (var entry in data.Completed)
        {
            if (entry.CompletedAt is null)
            {
                // 脏数据（无完成时间）：不移动、不丢弃
                remainingCompleted.Add(entry);
                continue;
            }

            var completionKey = LocalDate.KeyOfIso(entry.CompletedAt);
            if (completionKey == todayKey)
            {
                remainingCompleted.Add(entry);
                continue;
            }

            if (!archive.TryGetValue(completionKey, out var bucket))
            {
                bucket = new List<TodoItem>();
                archive[completionKey] = bucket;
            }

            bucket.Add(entry);
            archivedCount++;
        }

        var todos = OverdueService.RecalcActiveOverdue(data.Todos, todayKey);
        var markedOverdue = 0;
        for (var i = 0; i < todos.Count; i++)
        {
            if (todos[i].Overdue != data.Todos[i].Overdue)
            {
                markedOverdue++;
            }
        }

        var next = data with
        {
            Todos = todos,
            Completed = remainingCompleted,
            Archive = archive,
            UiState = data.UiState with { LastSettleDate = todayKey },
        };

        var changed = archivedCount > 0
            || markedOverdue > 0
            || data.UiState.LastSettleDate != todayKey;

        return new SettleResult(next, archivedCount, markedOverdue, changed);
    }
}
