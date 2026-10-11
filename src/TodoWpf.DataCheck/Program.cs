using System.Text;
using System.Text.Json;
using TodoWpf.Core.Models;
using TodoWpf.Core.Persistence;
using TodoWpf.Core.Services;
using TodoWpf.Core.Utils;

namespace TodoWpf.DataCheck;

/// <summary>
/// 只读验收探针（修订记录 R4）：读取 data.json 并打印条数统计 / 设置 / 窗口记忆 / 结算预演 / 迁移预演。
/// 全程只读——不写入、不改名、不迁移任何文件。
/// 与正式程序的唯一差异：遇到损坏档只报告，不做 .corrupt- 留档改名（正式程序会留档后按首次启动处理）。
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var io = new SystemFileIo();
        var explicitPath = args.Length > 0 ? args[0] : null;
        var newDataPath = DataPaths.NewDataFile();
        var legacyDataPath = DataPaths.LegacyDataFile();
        var now = DateTimeOffset.Now;
        var todayKey = LocalDate.Key(now);

        Console.WriteLine("桌面待办 · 数据只读校验（DataCheck）");
        Console.WriteLine("本工具只读：不写入、不改名、不迁移任何文件。");
        Console.WriteLine(new string('-', 66));
        Console.WriteLine($"校验日期：{todayKey}");
        Console.WriteLine();

        Console.WriteLine("【路径】");
        Console.WriteLine($"  新版数据文件：{newDataPath}（{(await io.ExistsAsync(newDataPath) ? "存在" : "不存在")}）");
        Console.WriteLine($"  旧版数据文件：{legacyDataPath}（{(await io.ExistsAsync(legacyDataPath) ? "存在" : "不存在")}）");
        Console.WriteLine();

        Console.WriteLine("【迁移预演】（正式程序首次启动时的行为；本工具不执行）");
        Console.WriteLine($"  {DescribeMigration(await LegacyMigration.PreviewAsync(io, legacyDataPath, newDataPath))}");
        Console.WriteLine();

        var file = explicitPath;
        if (file is null)
        {
            if (await io.ExistsAsync(newDataPath))
            {
                file = newDataPath;
            }
            else if (await io.ExistsAsync(legacyDataPath))
            {
                file = legacyDataPath;
            }
        }

        if (file is null)
        {
            Console.WriteLine("【读取】没有可读取的数据文件——等价于首次启动：空列表 + 引导文案。");
            return 0;
        }

        if (!File.Exists(file))
        {
            Console.WriteLine($"【读取】指定的文件不存在：{file}");
            return 0;
        }

        Console.WriteLine($"【读取】{file}");
        var before = new FileInfo(file);
        var text = ReadAllTextShared(file);

        AppData data;
        try
        {
            data = JsonCodec.Read(text);
        }
        catch (JsonException)
        {
            Console.WriteLine("  结果：解析失败（文件损坏或不是本程序的 data.json）。");
            Console.WriteLine("  正式程序的行为：改名 <文件名>.corrupt-<时间戳> 留档，然后按首次启动处理（空列表）。");
            Console.WriteLine("  本工具不做任何改名或写入——上面这个原文件保持原样。");
            return 0;
        }

        PrintStats(data, todayKey);
        PrintSettings(data);
        PrintWindowMemory(data);
        PrintSettlePreview(data, now, todayKey);

        var after = new FileInfo(file);
        var unchanged = before.LastWriteTimeUtc == after.LastWriteTimeUtc && before.Length == after.Length;
        Console.WriteLine("【只读校验】");
        Console.WriteLine($"  修改时间：{before.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} UTC（读取前/后{(unchanged ? "一致" : "不一致")}）");
        Console.WriteLine($"  文件大小：{after.Length} 字节");
        Console.WriteLine(unchanged ? "  文件未被修改 ✓" : "  警告：文件发生了变化，请报告！");
        PrintAcceptanceHint();
        return 0;
    }

    /// <summary>批次验收提示（中文由本工具输出：cmd 脚本本身保持纯 ASCII，避免控制台编码问题）。</summary>
    private static void PrintAcceptanceHint()
    {
        Console.WriteLine();
        Console.WriteLine("【批次 01（核心数据层）验收怎么看】");
        Console.WriteLine("  1. dotnet build → 出现“0 个错误”");
        Console.WriteLine("  2. dotnet test  → 出现“失败: 0”");
        Console.WriteLine("  3. 本探针 → 上面出现“文件未被修改 ✓”，且统计/设置与旧版一致");
        Console.WriteLine("  三项都满足即验收通过；有异常请把本窗口内容截图发给开发会话。");
    }

    private static void PrintStats(AppData data, string todayKey)
    {
        var activeOverdue = data.Todos.Count(t => OverdueService.IsActiveOverdue(t, todayKey));
        var completedToday = data.Completed.Count(
            t => t.CompletedAt is not null && LocalDate.KeyOfIso(t.CompletedAt) == todayKey);
        var archiveTotal = data.Archive.Values.Sum(bucket => bucket.Count);
        var earliest = data.Archive.Count == 0 ? null : data.Archive.Keys.Min();

        Console.WriteLine("【统计】");
        Console.WriteLine($"  待办：{data.Todos.Count} 条（上限 {data.Settings.MaxItems}；其中当前逾期 {activeOverdue} 条）");
        Console.WriteLine($"  已完成页：{data.Completed.Count} 条（其中完成于今天 {completedToday} 条）");
        Console.WriteLine(
            $"  历史归档：{data.Archive.Count} 天 / 共 {archiveTotal} 条" +
            (earliest is null ? "" : $"（最早 {earliest}）"));
        Console.WriteLine($"  模板：{data.Templates.Count} 条");
        Console.WriteLine();
    }

    private static void PrintSettings(AppData data)
    {
        Console.WriteLine("【设置】");
        Console.WriteLine(
            $"  时间制式：{(data.Settings.Hour12 ? "12 小时制" : "24 小时制")}；" +
            $"开机自启：{(data.Settings.AutoStart ? "开" : "关")}；" +
            $"完成排序：{(data.Settings.DoneSort == "desc" ? "倒序" : "正序")}");
        Console.WriteLine();
    }

    private static void PrintWindowMemory(AppData data)
    {
        var ui = data.UiState;
        var pos = ui.Pos is null ? "默认（首启为屏幕右侧偏上）" : $"({ui.Pos.X:0}, {ui.Pos.Y:0})";
        var childScales = ui.ChildScales.Count == 0
            ? "无"
            : string.Join("，", ui.ChildScales.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value:0.###}"));

        Console.WriteLine("【窗口记忆】");
        Console.WriteLine($"  位置：{pos}；主簇缩放：{ui.ClusterScale:0.###}；子窗缩放组：{childScales}");
        Console.WriteLine(
            $"  面板开关：快捷添加 {(ui.QuickVisible ? "开" : "关")} / 已完成 {(ui.DoneVisible ? "开" : "关")}；" +
            $"主题：{(ui.Theme == "night" ? "夜间" : "白天")}");
        Console.WriteLine(
            $"  置顶：{(ui.TopMost ? "开" : "关")}；缩进锁定：{(ui.TuckLock ? "开" : "关")}；" +
            $"最近结算日：{ui.LastSettleDate ?? "从未结算"}");
        Console.WriteLine();
    }

    private static void PrintSettlePreview(AppData data, DateTimeOffset now, string todayKey)
    {
        var settle = SettlementService.Settle(data, now);

        Console.WriteLine("【结算预演】（若此刻启动程序会发生什么；不改文件）");
        if (!settle.Changed)
        {
            Console.WriteLine("  无需结算：数据已是今天的结算状态。");
        }
        else
        {
            if (settle.ArchivedCount > 0)
            {
                var buckets = data.Completed
                    .Where(t => t.CompletedAt is not null && LocalDate.KeyOfIso(t.CompletedAt) != todayKey)
                    .GroupBy(t => LocalDate.KeyOfIso(t.CompletedAt))
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key}（{g.Count()} 条）");
                Console.WriteLine($"  将归档 {settle.ArchivedCount} 条 → {string.Join("、", buckets)}");
            }

            if (settle.MarkedOverdue > 0)
            {
                Console.WriteLine($"  将新标记 {settle.MarkedOverdue} 条待办为逾期");
            }

            if (settle.ArchivedCount == 0 && settle.MarkedOverdue == 0)
            {
                Console.WriteLine($"  仅更新最近结算日为 {settle.Data.UiState.LastSettleDate}");
            }
        }

        Console.WriteLine();
    }

    private static string DescribeMigration(MigrationStatus status) => status switch
    {
        MigrationStatus.Migrated => "首次启动将把旧档复制到新版路径（不删原件；新版已有数据则跳过）",
        MigrationStatus.SkippedTargetExists => "新版路径已有数据：不迁移（旧档保留不动）",
        MigrationStatus.SkippedNoLegacy => "无旧档：首次启动直接新建空数据",
        _ => "迁移预演失败",
    };

    /// <summary>共享读取（只读打开、不锁文件）。</summary>
    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
