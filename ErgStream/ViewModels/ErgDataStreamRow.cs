using CommunityToolkit.Mvvm.ComponentModel;
using ErgComm.Models;

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

        public void UpdateFromStatus(ErgStatus status)
        {
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
            TimeStamp = strokeData.Timestamp;
            ElapsedTime = strokeData.ElapsedTime;
            Distance = strokeData.Distance;
            Power = strokeData.Power;
            Calories = strokeData.Calories;
            ForceCurve = strokeData.ForceCurve;
        }
    }
}
