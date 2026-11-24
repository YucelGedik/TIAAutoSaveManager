using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TIAAutoSaveManager.App;

public sealed class AsyncCommand(Func<Task> action, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    private bool executing;
    public bool CanExecute(object? parameter) => !executing && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        executing = true; RaiseCanExecuteChanged();
        try { await action(); }
        finally { executing = false; RaiseCanExecuteChanged(); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class ParameterCommand(Action<object?> action) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action(parameter);
}
