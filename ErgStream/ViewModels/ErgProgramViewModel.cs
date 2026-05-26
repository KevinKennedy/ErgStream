using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
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

    public partial class ErgProgramViewModel : ObservableObject
    {

        private static readonly ErgProgramInterval[] ergProgramIntervals = new[]
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

        private readonly ErgRecorder ergRecorder; 
        private readonly IBeepService? beepService;

        private CancellationTokenSource? ergProgramCancellationTokenSource;
        List<double> allProgramPowers = new();
        private ErgDataStreamRow? currentStatus;
        private StringBuilder reportStringBuilder = new();

        private DateTime? recordingStartTime;

        [ObservableProperty]
        private ErgProgramState state = ErgProgramState.ErgDisconnected;

        [ObservableProperty]
        private TimeSpan intervalTimeRemaining = TimeSpan.Zero;

        [ObservableProperty]
        private string intervalTitle = string.Empty;

        [ObservableProperty]
        private ErgProgramIntervalType? intervalType;

        [ObservableProperty]
        private Color? indicatorColor = Colors.Transparent;

        [ObservableProperty]
        private TimeSpan? pace;

        [ObservableProperty]
        private double? strokeRate;

        public ErgProgramViewModel(ErgRecorder ergRecorder, IBeepService? beepService = null)
        {
            this.ergRecorder = ergRecorder;
            ergRecorder.OnNewDataReceived += OnNewErgRecorderDataReceived;
            ergRecorder.OnCleared += OnErgRecorderCleared;

            this.beepService = beepService;
        }

        private void OnNewErgRecorderDataReceived(bool isStatusMessage, bool isUpdate, ErgDataStreamRow row)
        {
            Debug.Assert(MainThread.IsMainThread);

            if (State == ErgProgramState.ErgDisconnected || State == ErgProgramState.ConnectingToErg)
            {
                State = ErgProgramState.ProgramNotStarted;
            }

            if (isStatusMessage)
            {
                currentStatus = row;
            }
        }

        private void OnErgRecorderCleared()
        {
            Debug.Assert(MainThread.IsMainThread);

            if (ergRecorder.ErgConnectionStatus == ErgConnectionStatus.Connected)
            {
                State = ErgProgramState.ProgramNotStarted;
            }
            else
            {
                State = ErgProgramState.ErgDisconnected;
            }

            StopProgram();
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
                    DateTime now = DateTime.UtcNow;
                    DateTime intervalEndTime = now + currentErgProgramInterval.Duration;

                    // fudge "now" so that we don't show the full duration at the start. Showing the full duration
                    // will look like a UI glitch because it only lasts for one tick.
                    now += TimeSpan.FromMilliseconds(1);
                    UpdateDisplayMembers(intervalEndTime - now,  currentErgProgramInterval.Title, currentErgProgramInterval.IntervalType, null, null);
                    int currentIntervalSecond = currentErgProgramInterval.Duration.Seconds;

                    if (currentErgProgramInterval.IntervalType == ErgProgramIntervalType.MaxEffort)
                    {
                        StartPowerRecording();
                    }

                    while (now < intervalEndTime)
                    {
                        TimeSpan? pace = (currentStatus != null && currentStatus.Pace != null) ? TimeSpan.FromSeconds(currentStatus.Pace.Value) : null;
                        double? strokeRate = currentStatus != null ? currentStatus.StrokeRate : null;
                        TimeSpan intervalTimeRemaining = intervalEndTime - now;
                        int newIntervalSecond = intervalTimeRemaining.Seconds;
                        if (newIntervalSecond != currentIntervalSecond)
                        {
                            DoBeep(currentErgProgramInterval.IntervalType, newIntervalSecond);
                            currentIntervalSecond = newIntervalSecond;
                        }
                        //UpdateDisplayMembers(intervalTimeRemaining, currentErgProgramInterval.Title, currentErgProgramInterval.IntervalType, pace, strokeRate);
                        await ergProgramTickTimer.WaitForNextTickAsync(token);
                        now = DateTime.UtcNow;
                    }

                    if (currentErgProgramInterval.IntervalType == ErgProgramIntervalType.MaxEffort)
                    {
                        EndPowerRecording(currentErgProgramInterval.Title);
                    }
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
                    IndicatorColor = intervalType switch
                    {
                        ErgProgramIntervalType.BuildToMaxEffort => Colors.Yellow,
                        ErgProgramIntervalType.MaxEffort => Colors.Green,
                        ErgProgramIntervalType.Recovery => Colors.Red,
                        ErgProgramIntervalType.CoolDown => Colors.Red,
                        _ => Colors.Transparent
                    };
                });
            }
        }

        private void DoBeep(ErgProgramIntervalType intervalType, int newIntervalSecond)
        {
            if (intervalType == ErgProgramIntervalType.BuildToMaxEffort)
            {
                if (newIntervalSecond <= 2 && newIntervalSecond >= 0)
                {
                    beepService?.Beep(BeepType.Prepare);
                }
            }
            else if (intervalType == ErgProgramIntervalType.MaxEffort)
            {
                if (newIntervalSecond == /*10*/ 9)
                {
                    beepService?.Beep(BeepType.Go);
                }
                else if (newIntervalSecond <= 2 && newIntervalSecond >= 0)
                {
                    beepService?.Beep(BeepType.Prepare);
                }
            }
            else if (intervalType == ErgProgramIntervalType.Recovery)
            {
                if (newIntervalSecond == /*27*/ 26)
                {
                    beepService?.Beep(BeepType.Stop);
                }
            }
            else if (intervalType == ErgProgramIntervalType.CoolDown)
            {
                if (newIntervalSecond == /*120*/119)
                {
                    beepService?.Beep(BeepType.Stop);
                }
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

            //reportStringBuilder.Clear();
            allProgramPowers.Clear();
            IntervalTimeRemaining = TimeSpan.Zero;
            IntervalTitle = string.Empty;
            IntervalType = null;
            IndicatorColor = Colors.Transparent;
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
            foreach (ErgDataStreamRow stroke in ergRecorder.StrokeMessages.Values)
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
    }
}