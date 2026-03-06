using CommunityToolkit.Mvvm.ComponentModel;
using ErgComm.Models;
using System.Text;

namespace ErgStream.ViewModels
{
    /// <summary>
    /// Basically a combination of ErgStatus and StrokeData fields, but with ObservableProperties for binding to the UI.
    /// </summary>
    public partial class ErgDataStreamRow : ObservableObject
    {
        [ObservableProperty]
        private DateTime timeStamp;

        /// <summary>
        /// Elapsed time in seconds.
        /// From the ergometer
        /// </summary>
        [ObservableProperty]
        private double? elapsedTime;

        /// <summary>
        /// Distance in meters.
        /// </summary>
        [ObservableProperty]
        private double? distance;

        /// <summary>
        /// Workout type (0=JustRowNoSplits, 1=JustRowSplits, 2=FixedDistanceNoSplits, etc.).
        /// </summary>
        [ObservableProperty]
        private int? workoutType;

        /// <summary>
        /// Workout state (0=WaitingToBegin, 1=WorkoutRowState, 2=CountDownPauseState, 
        /// 3=IntervalRestState, 4=WorkoutFinishedState, 5=TerminateState).
        /// </summary>
        [ObservableProperty]
        private int? workoutState;

        /// <summary>
        /// Stroke state (0=WaitingForWheelToReachMinSpeedState, 1=WaitingForWheelToAccelerateState, 
        /// 2=DrivingState, 3=DwellingAfterDriveState, 4=RecoveryState).
        /// </summary>
        [ObservableProperty]
        private StrokeState? strokeState;

        /// <summary>
        /// Current drag factor (0-255).
        /// </summary>
        [ObservableProperty]
        private int? dragFactor;

        /// <summary>
        /// Speed in m/sec
        /// </summary>
        [ObservableProperty]
        private double? speed;

        /// <summary>
        /// Current stroke rate (strokes per minute for rower).
        /// </summary>
        [ObservableProperty]
        private int? strokeRate;

        /// <summary>
        /// Heart rate in beats per minute (if available).
        /// </summary>
        [ObservableProperty]
        private int? heartRate;

        /// <summary>
        /// Current pace in seconds per 500m.
        /// </summary>
        [ObservableProperty]
        private double? pace;

        /// <summary>
        /// Current pace in seconds per 500m.
        /// </summary>
        [ObservableProperty]
        private double? averagePace;

        /// <summary>
        /// Current power output in watts.
        /// </summary>
        [ObservableProperty]
        private double? power;

        /// <summary>
        /// Calories per hour of the stroke
        /// </summary>
        [ObservableProperty]
        private int? calories;

        /// <summary>
        /// Power curve data points (force values over time during the stroke).
        /// </summary>
        [ObservableProperty]
        private int[]? forceCurve;

        public bool IsStrokeData { get; private set; } = false;

        public bool IsStatusData { get; private set; } = false;

        public void UpdateFromStatus(ErgStatus status)
        {
            IsStatusData = true;

            TimeStamp = status.Timestamp;
            ElapsedTime = status.ElapsedTime;
            Distance = status.Distance;
            WorkoutType = status.WorkoutType;
            WorkoutState = status.WorkoutState;
            StrokeState = status.StrokeState;
            DragFactor = status.DragFactor;
            Speed = status.Speed;
            StrokeRate = status.StrokeRate;
            HeartRate = status.HeartRate;
            Pace = status.Pace;
            AveragePace = status.AveragePace;
        }

        public void UpdateFromStroke(StrokeData strokeData)
        {
            IsStrokeData = true;

            TimeStamp = strokeData.Timestamp;
            ElapsedTime = strokeData.ElapsedTime;
            Distance = strokeData.Distance;
            Power = strokeData.Power;
            Calories = strokeData.Calories;
            ForceCurve = strokeData.ForceCurve;
        }

        public static string GetCsvHeader(bool includeStatusFields, bool includeStrokeFields)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Timestamp,ElapsedTime,Distance,");
            if (includeStatusFields)
            {
                sb.Append("WorkoutType,WorkoutState,StrokeState,DragFactor,Speed,StrokeRate,HeartRate,Pace,AveragePace,");
            }
            if (includeStrokeFields)
            {
                sb.Append("Power,Calories,ForceCurve");
            }
            return sb.ToString();
        }

        public string ToCsv(bool includeStatusFields, bool includeStrokeFields)
        {
            StringBuilder sb = new StringBuilder();

            // Format that Excel will recognize
            sb.Append(TimeStamp.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append(',');
            sb.Append(ElapsedTime?.ToString("F2") ?? "");
            sb.Append(',');
            sb.Append(Distance?.ToString("F2") ?? "");
            sb.Append(',');

            if (includeStatusFields)
            {
                sb.Append(WorkoutType?.ToString() ?? "");
                sb.Append(',');
                sb.Append(WorkoutState?.ToString() ?? "");
                sb.Append(',');
                sb.Append(StrokeState?.ToString() ?? "");
                sb.Append(',');
                sb.Append(DragFactor?.ToString() ?? "");
                sb.Append(',');
                sb.Append(Speed?.ToString() ?? "");
                sb.Append(',');
                sb.Append(StrokeRate?.ToString() ?? "");
                sb.Append(',');
                sb.Append(HeartRate?.ToString() ?? "");
                sb.Append(',');
                sb.Append(Pace?.ToString() ?? "");
                sb.Append(',');
                sb.Append(AveragePace?.ToString() ?? "");
            }
            if (includeStrokeFields)
            {
                sb.Append(Power?.ToString() ?? "");
                sb.Append(',');
                sb.Append(Calories?.ToString() ?? "");
                sb.Append(',');
                if (ForceCurve != null && ForceCurve.Length > 0)
                {
                    sb.Append(string.Join(",", ForceCurve)); // Use pipe as separator for force curve points
                }
                else
                {
                    sb.Append("");
                }
            }
            return sb.ToString();
        }
    }
}
