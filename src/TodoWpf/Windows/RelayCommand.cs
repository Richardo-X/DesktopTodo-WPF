using System.Windows.Input;

namespace TodoWpf.Windows;

/// <summary>轻量命令（各窗共用）：CanExecute + Execute 委托。</summary>
internal sealed class RelayCommand<T>(Func<T?, bool> can, Action<T?> run) : ICommand
    where T : class
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => can(parameter as T);

    public void Execute(object? parameter) => run(parameter as T);
}
