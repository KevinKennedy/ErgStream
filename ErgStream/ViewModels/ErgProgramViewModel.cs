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
        private Dictionary<int, ErgDataStreamRow> strokeMessages = new();
        private ErgDataStreamRow? currentStroke;

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
                State = ErgProgramState.ErgDisconnected;
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
                    currentStroke = existingRow;
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
            State = ErgProgramState.Running;

            try
            {

                foreach (var currentErgProgramInterval in ergProgramIntervals)
                {
                    DateTime intervalStartTime = DateTime.UtcNow;
                    DateTime intervalEndTime = intervalStartTime + currentErgProgramInterval.Duration;
                    IntervalTitle = currentErgProgramInterval.Title;
                    DateTime now = DateTime.UtcNow;

                    while (now > intervalEndTime)
                    {
                        IntervalTimeRemaining = intervalEndTime - now;
                        Pace = (currentStroke != null && currentStroke.Pace != null) ? TimeSpan.FromSeconds(currentStroke.Pace.Value) : null;
                        StrokeRate = currentStroke != null ? currentStroke.StrokeRate : null;
                        await ergProgramTickTimer.WaitForNextTickAsync(token);
                        now = DateTime.UtcNow;
                    }

                    IntervalTimeRemaining = TimeSpan.Zero;
                }

                State = ErgProgramState.Completed;
            }
            catch (OperationCanceledException)
            {
                State = ErgProgramState.Canceled;
            }
            finally
            {
                ;
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
        private async Task CopyAsync()
        {
            //if (DataRows.Count == 0)
            //{
            //    await Shell.Current.DisplayAlertAsync("No Data", "There is no data to copy.", "OK");
            //    return;
            //}



            StringBuilder sb = new();
            await Clipboard.SetTextAsync(sb.ToString());
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