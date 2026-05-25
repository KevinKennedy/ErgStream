using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErgComm;
using ErgComm.Models;
using System.Text;

namespace ErgStream.ViewModels
{
    public enum ErgProgramState
    {
        ErgDisconnected,
        ConnectingToErg,
        ProgramNotStarted,
        Running,
        Completed,
        Canceled,
    }

    public enum ErgProgramIntervalType
    {
        BuildToMaxEffort,
        MaxEffort,
        Recovery,
        CoolDown
    }

    public class ErgProgramInterval
    {
        public string Title { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }

        public ErgProgramIntervalType IntervalType { get; set; }
    }

    public partial class ErgProgramViewModel : ObservableObject, IQueryAttributable
    {
        private readonly ErgCommService ergCommService;
        private CancellationTokenSource? connectionCancellationTokenSource;

        private ErgProgramInterval[] ergProgramIntervals = new[]
        {
            new ErgProgramInterval { Title = "Build to max effort", Duration = TimeSpan.FromSeconds(3), IntervalType = ErgProgramIntervalType.BuildToMaxEffort },
            new ErgProgramInterval { Title = "Max effort 1", Duration = TimeSpan.FromSeconds(10), IntervalType = ErgProgramIntervalType.MaxEffort },
            new ErgProgramInterval { Title = "Recovery - no strokes, keep moving", Duration = TimeSpan.FromSeconds(27), IntervalType = ErgProgramIntervalType.Recovery },
            new ErgProgramInterval { Title = "Build to max effort", Duration = TimeSpan.FromSeconds(3), IntervalType = ErgProgramIntervalType.BuildToMaxEffort },
            new ErgProgramInterval { Title = "Max effort 2", Duration = TimeSpan.FromSeconds(10), IntervalType = ErgProgramIntervalType.MaxEffort },
            new ErgProgramInterval { Title = "Recovery - no strokes, keep moving", Duration = TimeSpan.FromSeconds(27), IntervalType = ErgProgramIntervalType.Recovery },
            new ErgProgramInterval { Title = "Build to max effort", Duration = TimeSpan.FromSeconds(3), IntervalType = ErgProgramIntervalType.BuildToMaxEffort },
            new ErgProgramInterval { Title = "Max effort 3", Duration = TimeSpan.FromSeconds(10), IntervalType = ErgProgramIntervalType.MaxEffort },
            new ErgProgramInterval { Title = "Recovery - no strokes, keep moving", Duration = TimeSpan.FromSeconds(27), IntervalType = ErgProgramIntervalType.Recovery },
            new ErgProgramInterval { Title = "Build to max effort", Duration = TimeSpan.FromSeconds(3), IntervalType = ErgProgramIntervalType.BuildToMaxEffort },
            new ErgProgramInterval { Title = "Max effort 4", Duration = TimeSpan.FromSeconds(10), IntervalType = ErgProgramIntervalType.MaxEffort },
            new ErgProgramInterval { Title = "Recovery - no strokes, keep moving", Duration = TimeSpan.FromSeconds(27), IntervalType = ErgProgramIntervalType.Recovery },
            new ErgProgramInterval { Title = "Build to max effort", Duration = TimeSpan.FromSeconds(3), IntervalType = ErgProgramIntervalType.BuildToMaxEffort },
            new ErgProgramInterval { Title = "Max effort 5", Duration = TimeSpan.FromSeconds(10), IntervalType = ErgProgramIntervalType.MaxEffort },
            new ErgProgramInterval { Title = "Recovery - no strokes, keep moving", Duration = TimeSpan.FromSeconds(27), IntervalType = ErgProgramIntervalType.Recovery },
            new ErgProgramInterval { Title = "Build to max effort", Duration = TimeSpan.FromSeconds(3), IntervalType = ErgProgramIntervalType.BuildToMaxEffort },
            new ErgProgramInterval { Title = "Max effort 6", Duration = TimeSpan.FromSeconds(10), IntervalType = ErgProgramIntervalType.MaxEffort },
            new ErgProgramInterval { Title = "Cool-down", Duration = TimeSpan.FromMinutes(2), IntervalType = ErgProgramIntervalType.CoolDown }
        };

        private CancellationTokenSource? ergProgramCancellationTokenSource;
        private Dictionary<int, ErgDataStreamRow> statusMessages = new();
        private Dictionary<int, ErgDataStreamRow> strokeMessages = new();
        List<double> allProgramPowers = new();
        private ErgDataStreamRow? currentStroke;
        private ErgDataStreamRow? currentStatus;
        private StringBuilder reportStringBuilder = new();

        private DateTime? recordingStartTime;

        [ObservableProperty]
        private string ergId = string.Empty;

        [ObservableProperty]
        private ErgProgramState state = ErgProgramState.ErgDisconnected;

        [ObservableProperty]
        private TimeSpan intervalTimeRemaining = TimeSpan.Zero;

        [ObservableProperty]
        private string intervalTitle = string.Empty;

        [ObservableProperty]
        private ErgProgramIntervalType? intervalType;

        [ObservableProperty]
        private TimeSpan? pace;

        [ObservableProperty]
        private double? strokeRate;

        public ErgProgramViewModel(ErgCommService ergCommService)
        {
            this.ergCommService = ergCommService;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ergId", out var ergIdObj) && ergIdObj is string ergId)
            {
                ErgId = ergId;
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

            State = ErgProgramState.ConnectingToErg;

            try
            {
                connectionCancellationTokenSource = new CancellationTokenSource();

                await ergCommService.ConnectToErgAsync(
                    ergId,
                    OnErgStatusDataReceived,
                    OnErgStrokeDataReceived,
                    connectionCancellationTokenSource.Token);
                
                State = ErgProgramState.ProgramNotStarted;
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel the connection - do nothing
                State = ErgProgramState.ErgDisconnected;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Connection Error",
                    $"Failed to connect to ergometer: {ex.Message}",
                    "OK");
                State = ErgProgramState.ErgDisconnected;
            }
        }

        private void OnErgStatusDataReceived(ErgStatus ergStatus)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (State == ErgProgramState.ConnectingToErg)
                {
                    State = ErgProgramState.ProgramNotStarted;
                }

                if (statusMessages.TryGetValue(ergStatus.StatusId, out var existingRow))
                {
                    existingRow.UpdateFromStatus(ergStatus);
                    currentStatus = existingRow;
                }
                else
                {
                    var newRow = new ErgDataStreamRow();
                    newRow.UpdateFromStatus(ergStatus);
                    statusMessages[ergStatus.StatusId] = newRow;
                    currentStatus = newRow;
                }
            });
        }

        private void OnErgStrokeDataReceived(StrokeData strokeData)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (State == ErgProgramState.ConnectingToErg)
                {
                    State = ErgProgramState.ProgramNotStarted;
                }

                if (strokeMessages.TryGetValue(strokeData.StrokeId, out var existingRow))
                {
                    existingRow.UpdateFromStroke(strokeData);
                    currentStroke = existingRow;
                }
                else
                {
                    var newRow = new ErgDataStreamRow();
                    newRow.UpdateFromStroke(strokeData);
                    strokeMessages[strokeData.StrokeId] = newRow;
                    currentStroke = newRow;
                }
            });
        }

        [RelayCommand]
        private async Task StartProgramAsync()
        {
            StopProgram();

            ergProgramCancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = ergProgramCancellationTokenSource.Token;
            PeriodicTimer ergProgramTickTimer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / 30.0));
            reportStringBuilder.Clear();
            allProgramPowers.Clear();
            UpdateState(ErgProgramState.Running);

            try
            {

                foreach (var currentErgProgramInterval in ergProgramIntervals)
                {
                    DateTime intervalStartTime = DateTime.UtcNow;
                    DateTime intervalEndTime = intervalStartTime + currentErgProgramInterval.Duration;
                    UpdateDisplayMembers(currentErgProgramInterval.Duration,  currentErgProgramInterval.Title, currentErgProgramInterval.IntervalType, null, null);
                    DateTime now = DateTime.UtcNow;

                    if(currentErgProgramInterval.IntervalType == ErgProgramIntervalType.MaxEffort)
                    {
                        StartPowerRecording();
                    }

                    while (now < intervalEndTime)
                    {
                        TimeSpan? pace = (currentStatus != null && currentStatus.Pace != null) ? TimeSpan.FromSeconds(currentStatus.Pace.Value) : null;
                        double? strokeRate = currentStatus != null ? currentStatus.StrokeRate : null;
                        UpdateDisplayMembers(intervalEndTime - now, currentErgProgramInterval.Title, currentErgProgramInterval.IntervalType, pace, strokeRate);
                        await ergProgramTickTimer.WaitForNextTickAsync(token);
                        now = DateTime.UtcNow;
                    }

                    if (currentErgProgramInterval.IntervalType == ErgProgramIntervalType.MaxEffort)
                    {
                        EndPowerRecording(currentErgProgramInterval.Title);
                    }


                    IntervalTimeRemaining = TimeSpan.Zero;
                }

                UpdateState(ErgProgramState.Completed);
            }
            catch (OperationCanceledException)
            {
                UpdateState(ErgProgramState.Canceled);
            }

            reportStringBuilder.AppendLine($"Mean power for all max effort strokes: {(allProgramPowers.Count > 0 ? allProgramPowers.Average().ToString("F2") : "N/A")}");

            void UpdateState(ErgProgramState newState)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    State = newState;
                });
            }

            void UpdateDisplayMembers(TimeSpan remaining, string title, ErgProgramIntervalType? intervalType, TimeSpan? pace, double? strokeRate)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IntervalTimeRemaining = remaining;
                    IntervalTitle = title;
                    IntervalType = intervalType;
                    Pace = pace;
                    StrokeRate = strokeRate;
                });
            }
        }

        [RelayCommand]
        private async Task StopProgramAsync()
        {
            StopProgram();
            await Task.CompletedTask;
        }

        private void StopProgram()
        {
            ergProgramCancellationTokenSource?.Cancel();
            ergProgramCancellationTokenSource?.Dispose();
            ergProgramCancellationTokenSource = null;
        }

        [RelayCommand]
        private async Task CopyReportAsync()
        {
            await Clipboard.SetTextAsync(reportStringBuilder.ToString());
        }

        private void StartPowerRecording()
        {
            recordingStartTime = DateTime.UtcNow;
        }

        private void EndPowerRecording(string intervalTitle)
        {
            DateTime recordingEndTime = DateTime.UtcNow;

            List<double> powers = new();

            reportStringBuilder.AppendLine($"Interval: {intervalTitle}");
            reportStringBuilder.AppendLine($"   StartTime: {(recordingStartTime.HasValue ? recordingStartTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : "null")}");
            reportStringBuilder.AppendLine($"   EndTime: {recordingEndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")}");
            reportStringBuilder.AppendLine($"   Stroke Powers:");
            foreach (var stroke in strokeMessages.Values)
            {
                if (stroke.IsStrokeData && stroke.TimeStamp >= recordingStartTime && stroke.TimeStamp <= recordingEndTime && stroke.Power.HasValue)
                {
                    powers.Add(stroke.Power.Value);
                    allProgramPowers.Add(stroke.Power.Value);
                    reportStringBuilder.AppendLine($"      Time: {stroke.TimeStamp.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}   Power: {stroke.Power.Value:F2}");
                }
            }
            reportStringBuilder.AppendLine($"   Mean Power: {(powers.Count > 0 ? powers.Average().ToString("F2") : "N/A")}");
            reportStringBuilder.AppendLine();

            recordingStartTime = null;
        }

        public void Disconnect()
        {
            StopProgram();

            connectionCancellationTokenSource?.Cancel();
            connectionCancellationTokenSource?.Dispose();
            connectionCancellationTokenSource = null;
        }
    }
}