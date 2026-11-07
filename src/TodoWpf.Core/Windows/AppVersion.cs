namespace TodoWpf.Core.Windows;

/// <summary>
/// SemVer 文本比较（C4 更新/降级检测用）：数值比较而非字典序（1.10.0 > 1.9.0）；
/// 缺失段按 0（"1.0" == "1.0.0"）；无效/null 整体按 0.0.0；段尾文字（"3-beta"）取段首数字。
/// </summary>
public static class AppVersion
{
    public static int Compare(string? a, string? b)
    {
        var av = ToTriple(a);
        var bv = ToTriple(b);
        for (var i = 0; i < 3; i++)
        {
            if (av[i] != bv[i])
            {
                return av[i] < bv[i] ? -1 : 1;
            }
        }

        return 0;
    }

    private static int[] ToTriple(string? version)
    {
        var result = new int[3];
        if (string.IsNullOrWhiteSpace(version))
        {
            return result;
        }

        var parts = version.Trim().TrimStart('v', 'V').Split('.');
        for (var i = 0; i < Math.Min(3, parts.Length); i++)
        {
            var digits = new string(parts[i].TakeWhile(char.IsDigit).ToArray());
            result[i] = int.TryParse(digits, out var n) ? n : 0;
        }

        return result;
    }
}
