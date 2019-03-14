using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace UpdateBuilder.Controls
{
    public class BusyIndicator : Control
    {
        #region Dependency Properties

        public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register("IsBusy", typeof(bool), typeof(BusyIndicator));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(BusyIndicator), new PropertyMetadata("Загрузка"));

        #endregion

        #region ctor

        static BusyIndicator()
        {
            FocusableProperty.OverrideMetadata(typeof(BusyIndicator), new FrameworkPropertyMetadata(false));
        }

        #endregion

        #region Properties

        public bool IsBusy
        {
            get { return (bool)GetValue(IsBusyProperty); }
            set { SetValue(IsBusyProperty, value); }
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        #endregion
    }
}
