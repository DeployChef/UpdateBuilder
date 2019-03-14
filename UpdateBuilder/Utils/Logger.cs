using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace UpdateBuilder.Utils
{
    public class Logger
    {
        private static object m_lock = new object();

        private ObservableCollection<string> _Log;
        private static Logger _instance;

        public ObservableCollection<string> Log
        {
            get
            {
                if (_Log == null)
                {
                    _Log = new ObservableCollection<string>();
                }
                return _Log;
            }
        }
        public static Logger Instance
        {
            get
            {
                // DoubleLock
                if (_instance == null)
                {
                    lock (m_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Logger();
                        }
                    }
                }
                return _instance;
            }
        }
       
        public void Clear()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Log.Clear();
                Log.Add("Путь ясен, лог чист");
            }));
        }

        public void Add(string logMessage)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => this.Log.Add(logMessage)));
        }
    }
}