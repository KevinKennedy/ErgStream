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
        private string _ergId = string.Empty;

        private ErgStatus? currentErgStatus = null;
        private StrokeData? currentStroke = null;

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
                    OnErgStatusDataReceived,
                    OnErgStrokeDataReceived,
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

        private void OnErgStatusDataReceived(ErgStatus ergStatus)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnecting = false;

                if (currentErgStatus == null)
                {
                    currentErgStatus = ergStatus;
                }
                else
                {
                    if (currentErgStatus.StatusId == ergStatus.StatusId)
                    {
                        // This is an update to the current status
                        currentErgStatus = ergStatus;

                        if (currentErgStatus.IsComplete())
                        {
                            WriteDataRow(currentErgStatus);
                            currentErgStatus = null;
                        }
                    }
                    else
                    {
                        // We've received a new status ID, so write out the previous, likely incomplete status
                        WriteDataRow(currentErgStatus);

                        currentErgStatus = ergStatus;
                    }
                }
            });
        }

        private void OnErgStrokeDataReceived(StrokeData strokeData)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnecting = false;

                if (currentStroke == null)
                {
                    currentStroke = strokeData;
                }
                else
                {
                    if (currentStroke.StrokeId == strokeData.StrokeId)
                    {
                        // This is an update to the current stroke
                        currentStroke = strokeData;

                        if (currentStroke.IsComplete())
                        {
                            WriteDataRow(currentStroke);
                            currentStroke = null;
                        }
                    }
                    else
                    {
                        // We've received a new stroke ID, so write out the previous, likely incomplete stroke
                        // Don't write this out if we haven't received both stroke state messages. Force curve is optional.
                        if (currentStroke.IsCompleteMinusForceCurve())
                        {
                            WriteDataRow(currentStroke);
                        }

                        currentStroke = strokeData;
                    }
                }
            });
        }

        private void WriteDataRow(object data)
        {
            if (_dataBuilder.Length == 0)
            {
                //_dataBuilder.AppendLine(data.GetCsvHeader());
            }

            System.Diagnostics.Debug.WriteLine(data.ToString());
            _dataBuilder.AppendLine(data.ToString());

            // Update the displayed text
            DataText = _dataBuilder.ToString();

            // Scroll to bottom
            DataScrollView.ScrollToAsync(DataLabel, ScrollToPosition.End, false);
        }

        private void OnClearClicked(object sender, EventArgs e)
        {
            _dataBuilder.Clear();
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