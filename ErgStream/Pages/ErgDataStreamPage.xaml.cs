using ErgComm;
using ErgComm.Models;
using System.Text;

namespace ErgStream.Pages
{
    [QueryProperty(nameof(ErgId), "ergId")]
    public partial class ErgDataStreamPage : ContentPage
    {
        private readonly ErgCommService _ergCommService;
        private CancellationTokenSource? _connectionCts;
        private readonly StringBuilder _dataBuilder;
        private bool _isHeaderWritten;
        private string _ergId = string.Empty;

        public string ErgId
        {
            get => _ergId;
            set
            {
                if (_ergId == value)
                {
                    return;
                }

                _ergId = value;
                OnPropertyChanged();

                _ = ConnectToErgAsync(_ergId);
            }
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                _isConnecting = value;
                OnPropertyChanged();
            }
        }

        private string _dataText = string.Empty;
        public string DataText
        {
            get => _dataText;
            set
            {
                _dataText = value;
                OnPropertyChanged();
            }
        }

        public ErgDataStreamPage()
        {
            InitializeComponent();
            
            _ergCommService = new ErgCommService();
            _dataBuilder = new StringBuilder();
            _isHeaderWritten = false;

            BindingContext = this;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        private async Task ConnectToErgAsync(string ergId)
        {
            if (_connectionCts != null)
            {
                _connectionCts.Cancel();
                _connectionCts.Dispose();
                _connectionCts = null;
            }

            if (string.IsNullOrEmpty(ergId))
            {
                return;
            }

            IsConnecting = true;

            try
            {
                _connectionCts = new CancellationTokenSource();

                await _ergCommService.ConnectToErgAsync(
                    ergId,
                    OnErgDataReceived,
                    _connectionCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel the connection - do nothing
            }
            catch (Exception ex)
            {
                await DisplayAlert("Connection Error", 
                    $"Failed to connect to ergometer: {ex.Message}", 
                    "OK");
            }
            finally
            {
                IsConnecting = false;
            }
        }

        private void OnErgDataReceived(ErgData data)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnecting = false;

                // Write CSV header on first data received
                if (!_isHeaderWritten)
                {
                    WriteHeader();
                    _isHeaderWritten = true;
                }

                // Write data row
                WriteDataRow(data);

                // Update the displayed text
                DataText = _dataBuilder.ToString();

                // Scroll to bottom
                DataScrollView.ScrollToAsync(DataLabel, ScrollToPosition.End, false);
            });
        }

        private void WriteHeader()
        {
            _dataBuilder.AppendLine("Timestamp,Elapsed Time (s),Distance (m),Stroke Rate (spm),Heart Rate (bpm),Pace (s/500m),Power (W),Calories,Drag Factor,Stroke State,Workout State");
        }

        private void WriteDataRow(ErgData data)
        {
            // TODO: Use ErgData.ToCSV()

            // Format timestamp as ISO 8601
            string timestamp = data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            
            // Format pace as mm:ss.t per 500m
            string pace = data.Pace.HasValue ? FormatPace(data.Pace.Value) : string.Empty;
            
            // Format optional values
            string heartRate = data.HeartRate?.ToString() ?? "";
            string dragFactor = data.DragFactor?.ToString() ?? "";
            string strokeState = data.StrokeState?.ToString() ?? "";

            _dataBuilder.AppendLine($"{timestamp},{data.ElapsedTime:F1},{data.Distance:F1},{data.StrokeRate},{heartRate},{pace},{data.Power:F1},{data.Calories},{dragFactor},{strokeState},{data.WorkoutState}");
        }

        private string FormatPace(double paceSeconds)
        {
            if (paceSeconds <= 0 || double.IsInfinity(paceSeconds) || double.IsNaN(paceSeconds))
                return "";

            int minutes = (int)(paceSeconds / 60);
            double seconds = paceSeconds % 60;
            return $"{minutes:D1}:{seconds:F1}";
        }

        private void OnClearClicked(object sender, EventArgs e)
        {
            _dataBuilder.Clear();
            _isHeaderWritten = false;
            DataText = string.Empty;
        }

        private async void OnCopyClicked(object sender, EventArgs e)
        {
            if (_dataBuilder.Length > 0)
            {
                await Clipboard.SetTextAsync(_dataBuilder.ToString());
                await AppShell.DisplayToastAsync("Data copied to clipboard");
            }
            else
            {
                await DisplayAlert("No Data", "There is no data to copy.", "OK");
            }
        }
    }
}