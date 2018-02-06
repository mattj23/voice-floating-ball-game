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
using FloatingBallGame.Audio;
using FloatingBallGame.ViewModels;

namespace FloatingBallGame.Views
{
    /// <summary>
    /// Interaction logic for ProviderSelectionView.xaml
    /// </summary>
    public partial class ProviderSelectionView : UserControl
    {
        public ProviderSelectionView()
        {
            InitializeComponent();
        }

        private void BeginButtonClicked(object sender, RoutedEventArgs e)
        {
            AppViewModel.Global.ConfigureAndStart();
        }

        private void CalibrateVolumeDevice(object sender, RoutedEventArgs e)
        {
            AppViewModel.Global.CalibrateDevice(AppViewModel.Global.Config.VolumeDevice, MeasurementType.Volume);
        }
    }
}
