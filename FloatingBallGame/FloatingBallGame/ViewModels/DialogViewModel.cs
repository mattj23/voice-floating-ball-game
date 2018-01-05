using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using FloatingBallGame.Annotations;

namespace FloatingBallGame.ViewModels
{
    public class DialogViewModel : INotifyPropertyChanged
    {
        private bool _isActive;
        private bool _isCancelable;
        private Brush _backgroundBrush;
        private string _title;
        private string _message;
        private Action _okAction;
        private Action _cancelAction;


        private ConcurrentQueue<DialogInformation> _displayRequests;

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (value == _isActive) return;
                _isActive = value;
                RaisePropertyChanged();
            }
        }

        public bool IsCancelable
        {
            get { return _isCancelable; }
            set
            {
                if (value == _isCancelable) return;
                _isCancelable = value;
                RaisePropertyChanged();
            }
        }

        public Brush BackgroundBrush
        {
            get { return _backgroundBrush; }
            set
            {
                if (Equals(value, _backgroundBrush)) return;
                _backgroundBrush = value;
                RaisePropertyChanged();
            }
        }

        public string Title
        {
            get { return _title; }
            set
            {
                if (value == _title) return;
                _title = value;
                RaisePropertyChanged();
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                if (value == _message) return;
                _message = value;
                RaisePropertyChanged();
            }
        }

        public Action OkAction
        {
            get { return _okAction; }
            set
            {
                if (Equals(value, _okAction)) return;
                _okAction = value;
                RaisePropertyChanged();
            }
        }

        public Action CancelAction
        {
            get { return _cancelAction; }
            set
            {
                if (Equals(value, _cancelAction)) return;
                _cancelAction = value;
                RaisePropertyChanged();
            }
        }

        public DialogViewModel()
        {
            this._displayRequests = new ConcurrentQueue<DialogInformation>();
        }

        /// <summary>
        /// Enqueues a dialog show request in the current viewmodel request list
        /// </summary>
        /// <param name="title"></param>
        /// <param name="message"></param>
        /// <param name="okAction"></param>
        /// <param name="cancelAction"></param>
        /// <param name="backgroundBrush"></param>
        /// <param name="canCancel"></param>
        public void Show(string title, string message, Action okAction, Action cancelAction, Brush backgroundBrush = null, bool canCancel = true)
        {
            this._displayRequests.Enqueue(new DialogInformation
            {
                Title = title,
                Message = message,
                OkAction = okAction,
                CancelAction = cancelAction,
                BackgroundBrush = backgroundBrush,
                CanCancel = canCancel
            });

            if (!this.IsActive)
                ShowFromQueue();
        }

        private void ShowFromQueue()
        {
            // Attempt a dequeue
            DialogInformation info = null;
            var result = this._displayRequests.TryDequeue(out info);

            if (result)
            {
                // Display the item from the queue
                if (info.BackgroundBrush == null)
                    this.BackgroundBrush = new SolidColorBrush(Colors.WhiteSmoke);
                else
                    this.BackgroundBrush = info.BackgroundBrush;

                this.Title = info.Title;
                this.Message = info.Message;
                this.OkAction = info.OkAction;
                this.CancelAction = info.CancelAction;
                this.IsCancelable = info.CanCancel;
                this.IsActive = true;
            }
            else
            {
                // Set isactive = false
                this.IsActive = false;
            }


        }

        public void ShowOkOnly(string title, string message, Action okAction, Brush backgroundBrush = null)
        {
            this.Show(title, message, okAction, null, backgroundBrush, false);
        }

        public void OkButtonClick()
        {
            // invoke the action associated with the OK button
            this.OkAction?.Invoke();

            // See if there's another message in the queue
            this.ShowFromQueue();
        }

        public void CancelButtonClick()
        {
            // Invoke the action associated with the cancel button
            this.CancelAction?.Invoke();

            // See if there's another message in the queue
            this.ShowFromQueue();
        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}