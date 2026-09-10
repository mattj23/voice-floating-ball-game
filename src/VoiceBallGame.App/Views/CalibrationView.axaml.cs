using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoiceBallGame.App.Views;

public partial class CalibrationView : UserControl
{
    public CalibrationView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
