using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoiceBallGame.App.Views;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
