using System;
using System.Diagnostics;
using System.Windows.Input;

namespace UpdateBuilder.ViewModels.Base
{
    public class RelayCommand : ICommand
    {
        readonly Action<Object> _execute;
        readonly Predicate<Object> _canExecute;

        public RelayCommand(Action<Object> execute, Predicate<Object> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            _execute = execute;
            _canExecute = canExecute;
        }

        [DebuggerStepThrough]
        public Boolean CanExecute(Object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(Object parameter = null)
        {
            _execute(parameter);
        }
    }
}
