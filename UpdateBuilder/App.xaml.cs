using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using UpdateBuilder.ViewModels;
using UpdateBuilder.Views;

namespace UpdateBuilder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var view = new MainWindow();
            var vm = new MainWindowViewModel();
            view.DataContext = vm;
            Current.MainWindow = view;
            view.Show();
        }
    }
}
