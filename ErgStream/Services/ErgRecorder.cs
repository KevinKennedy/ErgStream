using ErgComm;
using ErgComm.Models;
using ErgStream.ViewModels;
using System.Diagnostics;

namespace ErgStream.Services
{
    public enum ErgConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    public delegate void ErgDataReceivedEventHandler(bool isStatusMessage, bool isUpdate, ErgDataStreamRow row);


    public class ErgRecorder
    {
        private readonly ErgCommService ergCommService;
        private CancellationTokenSource? connectionCancellationTokenSource;

        private Dictionary<int, ErgDataStreamRow> statusMessages = new();
        private Dictionary<int, ErgDataStreamRow> strokeMessages = new();
        private List<ErgDataStreamRow> allMessagesByTime = new();

        public IReadOnlyDictionary<int, ErgDataStreamRow> StatusMessages => statusMessages;
        public IReadOnlyDictionary<int, ErgDataStreamRow> StrokeMessages => strokeMessages;
        public IReadOnlyList<ErgDataStreamRow> AllMessagesByTime => allMessagesByTime;
        public ErgConnectionStatus ErgConnectionStatus { get; private set; } = ErgConnectionStatus.Disconnected;

        public event ErgDataReceivedEventHandler? OnNewDataReceived;
        public event Action? OnCleared;

        public ErgRecorder(ErgCommService ergCommService)
        {
            this.ergCommService = ergCommService;
        }

        public async Task ConnectToErgAsync(string ergId)
        {
            if (connectionCancellationTokenSource != null)
            {
                connectionCancellationTokenSource.Cancel();
                connectionCancellationTokenSource.Dispose();
                connectionCancellationTokenSource = null;
                ErgConnectionStatus = ErgConnectionStatus.Disconnected;
            }

            if (string.IsNullOrEmpty(ergId))
            {
                return;
            }

            ErgConnectionStatus = ErgConnectionStatus.Connecting;

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
                ErgConnectionStatus = ErgConnectionStatus.Disconnected;
                // Expected when we cancel the connection - do nothing
            }
            catch (Exception ex)
            {
                ErgConnectionStatus = ErgConnectionStatus.Error;
                await Shell.Current.DisplayAlertAsync("Connection Error",
                    $"Failed to connect to ergometer: {ex.Message}",
                    "OK");
            }
        }

        private void OnErgStatusDataReceived(ErgStatus ergStatus)
        {

            // Need to run on main thread since we update things that
            // cause UI changes.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ErgConnectionStatus = ErgConnectionStatus.Connected;

                if (statusMessages.TryGetValue(ergStatus.StatusId, out var existingRow))
                {
                    existingRow.UpdateFromStatus(ergStatus);
                    SendUpdate(isStatusMessage: true, isUpdate: true, row: existingRow);
                }
                else
                {
                    var newRow = new ErgDataStreamRow();
                    newRow.UpdateFromStatus(ergStatus);
                    statusMessages[ergStatus.StatusId] = newRow;
                    allMessagesByTime.Add(newRow);
                    SendUpdate(isStatusMessage: true, isUpdate: false, row: newRow);
                }
            });
        }

        private void OnErgStrokeDataReceived(StrokeData strokeData)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (strokeMessages.TryGetValue(strokeData.StrokeId, out var existingRow))
                {
                    existingRow.UpdateFromStroke(strokeData);
                    SendUpdate(isStatusMessage: false, isUpdate: true, row: existingRow);
                }
                else
                {
                    var newRow = new ErgDataStreamRow();
                    newRow.UpdateFromStroke(strokeData);
                    strokeMessages[strokeData.StrokeId] = newRow;
                    allMessagesByTime.Add(newRow);
                    SendUpdate(isStatusMessage: false, isUpdate: false, row: newRow);
                }
            });
        }

        private void SendUpdate(bool isStatusMessage, bool isUpdate, ErgDataStreamRow row)
        {
            Debug.Assert(MainThread.IsMainThread);

            if (OnNewDataReceived == null)
            {
                return;
            }

            OnNewDataReceived.Invoke(isStatusMessage, isUpdate, row);
        }

        public void Clear()
        {
            Debug.Assert(MainThread.IsMainThread);

            allMessagesByTime.Clear();
            statusMessages.Clear();
            strokeMessages.Clear();
            OnCleared?.Invoke();
        }
    }
}
