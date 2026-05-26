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

    public partial class ErgDataStreamViewModel : ObservableObject
    {
        private const string ErgDataFilterPreferenceKey = "ErgDataStream_DataFilter";
        private const string AutoScrollEnabledPreferenceKey = "ErgDataStream_AutoScrollEnabled";

        private readonly ErgRecorder ergRecorder;
        private readonly HashSet<ErgDataStreamRow> visibleRows = new();

        [ObservableProperty]
        private bool isConnecting;

        [ObservableProperty]
        private ErgDataFilter ergDataFilter = ErgDataFilter.All;

        [ObservableProperty]
        private bool autoScrollEnabled = true;

        [ObservableProperty]
        private ObservableCollection<ErgDataStreamRow> dataRows = new();

        public ErgDataStreamViewModel(ErgRecorder ergRecorder)
        {
            this.ergRecorder = ergRecorder;
            ergRecorder.OnNewDataReceived += OnNewErgRecorderDataReceived;
            RestoreErgDataFilter();
            RestoreAutoScrollEnabled();
            ergRecorder.OnCleared += OnErgRecorderCleared;
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

        private void RestoreAutoScrollEnabled()
        {
            AutoScrollEnabled = Preferences.Get(AutoScrollEnabledPreferenceKey, true);
        }

        partial void OnAutoScrollEnabledChanged(bool value)
        {
            Preferences.Set(AutoScrollEnabledPreferenceKey, value);
        }

        private void OnNewErgRecorderDataReceived(bool isStatusMessage, bool isUpdate, ErgDataStreamRow row)
        {
            IsConnecting = false;

            if (isStatusMessage)
            {
                if (!isUpdate)
                {
                    if (IsVisible(row))
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            DataRows.Add(row);
                            visibleRows.Add(row);
                        });

                    }
                }
            }
            else // stroke message
            {
                if (isUpdate)
                {
                    bool wasVisible = visibleRows.Contains(row);
                    
                    if (wasVisible && !IsVisible(row))
                    {
                        DataRows.Remove(row);
                        visibleRows.Remove(row);
                    }
                    else if (!wasVisible && IsVisible(row))
                    {
                        DataRows.Add(row);
                        visibleRows.Add(row);
                    }
                }
                else
                {
                    if (IsVisible(row))
                    {
                        DataRows.Add(row);
                        visibleRows.Add(row);
                    }
                }
            }
        }
        private void OnErgRecorderCleared()
        {
            DataRows.Clear();
            visibleRows.Clear();
        }

        partial void OnErgDataFilterChanged(ErgDataFilter value)
        {
            SaveErgDataFilter();

            IEnumerable<ErgDataStreamRow> newDataRowsEnum = ergRecorder.AllMessagesByTime.Where(row => IsVisible(row));

            ObservableCollection<ErgDataStreamRow> newDataRows = new();
            visibleRows.Clear();
            foreach (var row in newDataRowsEnum)
            {
                newDataRows.Add(row);
                visibleRows.Add(row);
            }

            DataRows = newDataRows;
        }

        [RelayCommand]
        private async Task ClearAsync()
        {
            bool confirm = await Shell.Current.DisplayAlertAsync(
                "Clear Data",
                "Are you sure you want to clear all data? Any data you haven't copied will be permanently deleted.",
                "Yes",
                "No");

            if (!confirm)
            {
                return;
            }

            ergRecorder.Clear();
        }

        [RelayCommand]
        private async Task CopyAsync()
        {
            if (DataRows.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("No Data", "There is no data to copy.", "OK");
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