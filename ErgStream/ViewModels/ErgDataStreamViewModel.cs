using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErgComm;
using ErgComm.Models;
using System.Collections.ObjectModel;
using System.Text;

namespace ErgStream.ViewModels
{
    public enum ErgDataFilter
    {
        All,
        StrokeOnly,
        StrokeWithPowerOnly
    }

    public partial class ErgDataStreamViewModel : ObservableObject, IQueryAttributable
    {
        private const string ErgDataFilterPreferenceKey = "ErgDataStream_DataFilter";

        private readonly ErgCommService ergCommService;
        private CancellationTokenSource? connectionCancellationTokenSource;

        [ObservableProperty]
        private string ergId = string.Empty;

        [ObservableProperty]
        private bool isConnecting;

        [ObservableProperty]
        private ErgDataFilter ergDataFilter = ErgDataFilter.All;

        [ObservableProperty]
        private ObservableCollection<ErgDataStreamRow> dataRows = new();

        private Dictionary<int, ErgDataStreamRow> statusMessages = new();
        private Dictionary<int, ErgDataStreamRow> strokeMessages = new();

        public ErgDataStreamViewModel(ErgCommService ergCommService)
        {
            this.ergCommService = ergCommService;
            RestoreErgDataFilter();
        }

        private void RestoreErgDataFilter()
        {
            var savedFilter = Preferences.Get(ErgDataFilterPreferenceKey, nameof(ErgDataFilter.All));
            if (Enum.TryParse<ErgDataFilter>(savedFilter, out var filter))
            {
                ErgDataFilter = filter;
            }
        }

        private void SaveErgDataFilter()
        {
            Preferences.Set(ErgDataFilterPreferenceKey, ErgDataFilter.ToString());
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
            if (connectionCancellationTokenSource != null)
            {
                connectionCancellationTokenSource.Cancel();
                connectionCancellationTokenSource.Dispose();
                connectionCancellationTokenSource = null;
            }

            if (string.IsNullOrEmpty(ergId))
            {
                return;
            }

            IsConnecting = true;

            try
            {
                connectionCancellationTokenSource = new CancellationTokenSource();

                await ergCommService.ConnectToErgAsync(
                    ergId,
                    OnErgStatusDataReceived,
                    OnErgStrokeDataReceived,
                    connectionCancellationTokenSource.Token);
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

                if (statusMessages.TryGetValue(ergStatus.StatusId, out var existingRow))
                {
                    existingRow.UpdateFromStatus(ergStatus);
                }
                else
                {
                    var newRow = new ErgDataStreamRow();
                    newRow.UpdateFromStatus(ergStatus);
                    statusMessages[ergStatus.StatusId] = newRow;
                    if (IsVisible(newRow))
                    {
                        DataRows.Add(newRow);
                    }
                }
            });
        }

        private void OnErgStrokeDataReceived(StrokeData strokeData)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsConnecting = false;

                if (strokeMessages.TryGetValue(strokeData.StrokeId, out var existingRow))
                {
                    bool wasVisible = IsVisible(existingRow);

                    existingRow.UpdateFromStroke(strokeData);

                    if (wasVisible && !IsVisible(existingRow))
                    {
                        DataRows.Remove(existingRow);
                    }
                    else if (!wasVisible && IsVisible(existingRow))
                    {
                        DataRows.Add(existingRow);
                    }
                }
                else
                {
                    var newRow = new ErgDataStreamRow();
                    newRow.UpdateFromStroke(strokeData);
                    strokeMessages[strokeData.StrokeId] = newRow;
                    if (IsVisible(newRow))
                    {
                        DataRows.Add(newRow);
                    }
                }
            });
        }

        partial void OnErgDataFilterChanged(ErgDataFilter value)
        {
            SaveErgDataFilter();

            IEnumerable<ErgDataStreamRow> newDataRowsEnum = statusMessages.Values.Concat(strokeMessages.Values).OrderBy(row => row.TimeStamp).Where(row => IsVisible(row));

            ObservableCollection<ErgDataStreamRow> newDataRows = new();
            foreach (var row in newDataRowsEnum)
            {
                newDataRows.Add(row);
            }

            DataRows = newDataRows;
        }

        [RelayCommand]
        private async Task ClearAsync()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Clear Data",
                "Are you sure you want to clear all data? Any data you haven't copied will be permanently deleted.",
                "Yes",
                "No");

            if (!confirm)
            {
                return;
            }

            statusMessages.Clear();
            strokeMessages.Clear();
            DataRows.Clear();
        }

        [RelayCommand]
        private async Task CopyAsync()
        {
            if (DataRows.Count == 0)
            {
                await Shell.Current.DisplayAlert("No Data", "There is no data to copy.", "OK");
                return;
            }

            bool includeStatus = false;
            bool includeStrokes = false;
            if (ErgDataFilter == ErgDataFilter.All)
            {
                includeStatus = true;
                includeStrokes = true;
            }
            else if (ErgDataFilter == ErgDataFilter.StrokeOnly || ErgDataFilter == ErgDataFilter.StrokeWithPowerOnly)
            {
                includeStrokes = true;
            }

            StringBuilder sb = new();
            sb.AppendLine(ErgDataStreamRow.GetCsvHeader(includeStatus, includeStrokes));
            foreach (ErgDataStreamRow row in DataRows)
            {
                sb.AppendLine(row.ToCsv(includeStatus, includeStrokes));
            }

            await Clipboard.SetTextAsync(sb.ToString());
        }

        public void Disconnect()
        {
            connectionCancellationTokenSource?.Cancel();
            connectionCancellationTokenSource?.Dispose();
            connectionCancellationTokenSource = null;
        }

        private bool IsVisible(ErgDataStreamRow row)
        {
            if (ErgDataFilter == ErgDataFilter.All)
            {
                return true;
            }
            else if (ErgDataFilter == ErgDataFilter.StrokeOnly)
            {
                return row.IsStrokeData;
            }
            else if (ErgDataFilter == ErgDataFilter.StrokeWithPowerOnly)
            {
                return row.IsStrokeData && row.Power.HasValue;
            }
            else
            {
                throw new InvalidDataException($"Invalid data filter value {ErgDataFilter}");
            }
        }
    }
}