using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace PalMake
{
    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly FileProcessingService _fileProcessingService;
        private bool _isProcessing;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _fileProcessingService = new FileProcessingService();
        }


        private string? _myLabelText;
        public string? MyLabelText
        {
            get => _myLabelText;
            set
            {
                _myLabelText = value;
                OnPropertyChanged("MyLabelText");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void BtnRun_OnClick(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            _isProcessing = true;
            BtnRun.IsEnabled = false;
            var paths = new List<string> { InputPath.Text, OutputPath.Text };
            await _fileProcessingService.WorkerOnDoWork(paths, this);
            _isProcessing = false;
            BtnRun.IsEnabled = true;
        }
    }
}
