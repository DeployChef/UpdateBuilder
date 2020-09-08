﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
            this.Dispatcher.UnhandledException += DispatcherOnUnhandledException;

            var view = new MainWindow();
            var vm = new MainWindowViewModel();
            view.DataContext = vm;
            Current.MainWindow = view;
            view.Show();

        }

        private void DispatcherOnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = false;

            ShowUnhandledException(e);
        }

        void ShowUnhandledException(DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            string errorMessage = string.Format(
                "An application error occurred.\nPlease check whether your data is correct and repeat the action. If this error occurs again there seems to be a more serious malfunction in the application, and you better close it.\n\nError: {0}\n\nDo you want to continue?\n(if you click Yes you will continue with your work, if you click No the application will close)",

                e.Exception.Message + (e.Exception.InnerException != null
                    ? "\n" +
                      e.Exception.InnerException.Message
                    : null));

            if (MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Error) == MessageBoxResult.No)
            {
                if (MessageBox.Show(
                        "WARNING: The application will close. Any changes will not be saved!\nDo you really want to close it?",
                        "Close the application!", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) ==
                    MessageBoxResult.Yes)
                {
                    Application.Current.Shutdown();
                }
            }
        }
    }
}
