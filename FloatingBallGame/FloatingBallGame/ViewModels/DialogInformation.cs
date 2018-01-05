using System;
using System.Windows.Media;

namespace FloatingBallGame.ViewModels
{
    public class DialogInformation
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public Action OkAction { get; set; }
        public Action CancelAction { get; set; }
        public Brush BackgroundBrush { get; set; }
        public bool CanCancel { get; set; }
    }
}