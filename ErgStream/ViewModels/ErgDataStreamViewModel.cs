using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErgComm;
using ErgComm.Models;
using System.Text;

namespace ErgStream.ViewModels
{
    public partial class ErgDataStreamViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ErgCommService _ergCommService;
        private CancellationTokenSource? _connectionCts;
        private readonly StringBuilder _dataBuilder;
        
        private ErgStatus? _currentErgStatus;
        private StrokeData? _currentStroke;

        [ObservableProperty]
        private string _ergId = string.Empty;

        [ObservableProperty]
        private bool _isConnecting;

        [ObservableProperty]
        private string _dataText = string.Empty;

        public ErgDataStreamViewModel(ErgCommService ergCommService)
        {
            _ergCommService = ergCommService;
            _dataBuilder = new StringBuilder();
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ergId", out var ergIdObj) && ergIdObj is string ergId)
            {
                ErgId = ergId;
                _ = ConnectToErgAsync(ergId);
            }
        }

        partial void OnErgIdChanged(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _ = ConnectToErgAsync(value);
            }
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
                await Shell.Current.DisplayAlert("Connection Error",
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

                if (_currentErgStatus == null)
                {
                    _currentErgStatus = ergStatus;
                }
                else
                {
                    if (_currentErgStatus.StatusId == ergStatus.StatusId)
                    {
                        // This is an update to the current status
                        _currentErgStatus = ergStatus;

                        if (_currentErgStatus.IsComplete())
                        {
                            WriteDataRow(_currentErgStatus);
                            _currentErgStatus = null;
                        }
                    }
                    else
                    {
                        // We've received a new status ID, so write out the previous, likely incomplete status
                        WriteDataRow(_currentErgStatus);

                        _currentErgStatus = ergStatus;
                    }
                }
            });
        }

        private void OnErgStrokeDataReceived(StrokeData strokeData)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnecting = false;

                if (_currentStroke == null)
                {
                    _currentStroke = strokeData;
                }
                else
                {
                    if (_currentStroke.StrokeId == strokeData.StrokeId)
                    {
                        // This is an update to the current stroke
                        _currentStroke = strokeData;

                        if (_currentStroke.IsComplete())
                        {
                            WriteDataRow(_currentStroke);
                            _currentStroke = null;
                        }
                    }
                    else
                    {
                        // We've received a new stroke ID, so write out the previous, likely incomplete stroke
                        // Don't write this out if we haven't received both stroke state messages. Force curve is optional.
                        if (_currentStroke.IsCompleteMinusForceCurve())
                        {
                            WriteDataRow(_currentStroke);
                        }

                        _currentStroke = strokeData;
                    }
                }
            });
        }

        private void WriteDataRow(object data)
        {
            System.Diagnostics.Debug.WriteLine(data.ToString());
            _dataBuilder.AppendLine(data.ToString());

            // Update the displayed text
            DataText = _dataBuilder.ToString();
        }

        [RelayCommand]
        private void Clear()
        {
            _dataBuilder.Clear();
            DataText = string.Empty;
        }

        [RelayCommand]
        private async Task CopyAsync()
        {
            if (_dataBuilder.Length > 0)
            {
                await Clipboard.SetTextAsync(_dataBuilder.ToString());
                await AppShell.DisplayToastAsync("Data copied to clipboard");
            }
            else
            {
                await Shell.Current.DisplayAlert("No Data", "There is no data to copy.", "OK");
            }
        }

        public void Disconnect()
        {
            _connectionCts?.Cancel();
            _connectionCts?.Dispose();
            _connectionCts = null;
        }
    }
}