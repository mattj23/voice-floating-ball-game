using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FloatingBallGame.ViewModels;

namespace FloatingBallGame.Views
{
    /// <summary>
    /// Interaction logic for CalibrationView.xaml
    /// </summary>
    public partial class CalibrationView : UserControl
    {
        public static readonly DependencyProperty CalibrationProperty = DependencyProperty.Register(
            "Calibration", typeof(CalibrationViewModel), typeof(CalibrationView), new PropertyMetadata(default(CalibrationViewModel)));

        public CalibrationViewModel Calibration
        {
            get { return (CalibrationViewModel) GetValue(CalibrationProperty); }
            set { SetValue(CalibrationProperty, value); }
        }

        public CalibrationView()
        {
            InitializeComponent();
            this.LayoutRoot.DataContext = this;
        }
    }
}
