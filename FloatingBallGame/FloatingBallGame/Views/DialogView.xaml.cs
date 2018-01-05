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
    /// Interaction logic for DialogView.xaml
    /// </summary>
    public partial class DialogView : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(DialogViewModel), typeof(DialogView), new PropertyMetadata(default(DialogViewModel)));

        public static readonly DependencyProperty DialogWidthProperty = DependencyProperty.Register(
            "DialogWidth", typeof(double), typeof(DialogView), new PropertyMetadata(default(double)));

        public double DialogWidth
        {
            get { return (double)GetValue(DialogWidthProperty); }
            set { SetValue(DialogWidthProperty, value); }
        }

        public DialogViewModel ViewModel
        {
            get { return (DialogViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        public DialogView()
        {
            InitializeComponent();
            ((FrameworkElement)this.Content).DataContext = this;

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.ViewModel.CancelButtonClick();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            this.ViewModel.OkButtonClick();
        }
    }
}
