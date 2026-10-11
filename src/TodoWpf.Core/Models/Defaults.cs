namespace TodoWpf.Core.Models;

/// <summary>默认档（需求文档 §2.8/§2.9）：首次启动空列表、上限 10、24 小时制、不自启、排序正序；面板全开、白天主题。</summary>
public static class Defaults
{
    public static AppSettings DefaultSettings() => new();

    public static UiState DefaultUiState() => new();

    public static AppData DefaultAppData() => new();
}
