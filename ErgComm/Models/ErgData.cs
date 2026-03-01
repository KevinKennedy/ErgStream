using System;
using System.Collections.Generic;

namespace ErgComm.Models
{
    /// <summary>
    /// Represents real-time data from an ergometer.
    /// </summary>
    public class ErgData
    {
        /// <summary>
        /// Timestamp when the data was captured. In local time for easier display, but could be converted to UTC if needed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Elapsed time in seconds.
        /// </summary>
        public double ElapsedTime { get; set; }

        /// <summary>
        /// Distance in meters.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Current stroke rate (strokes per minute for rower).
        /// </summary>
        public int StrokeRate { get; set; }

        /// <summary>
        /// Heart rate in beats per minute (if available).
        /// </summary>
        public int? HeartRate { get; set; }

        /// <summary>
        /// Current pace in seconds per 500m.
        /// </summary>
        public double Pace { get; set; }

        /// <summary>
        /// Current power output in watts.
        /// </summary>
        public double Power { get; set; }

        /// <summary>
        /// Total calories burned.
        /// </summary>
        public int Calories { get; set; }

        /// <summary>
        /// Current drag factor (0-255).
        /// </summary>
        public int? DragFactor { get; set; }

        /// <summary>
        /// Stroke state (0=WaitingForWheelToReachMinSpeedState, 1=WaitingForWheelToAccelerateState, 
        /// 2=DrivingState, 3=DwellingAfterDriveState, 4=RecoveryState).
        /// </summary>
        public int? StrokeState { get; set; }

        /// <summary>
        /// Power curve data points (force values over time during the stroke).
        /// </summary>
        public List<int>? PowerCurve { get; set; }

        /// <summary>
        /// Workout state (0=WaitingToBegin, 1=WorkoutRowState, 2=CountDownPauseState, 
        /// 3=IntervalRestState, 4=WorkoutFinishedState, 5=TerminateState).
        /// </summary>
        public int WorkoutState { get; set; }

        /// <summary>
        /// Workout type (0=JustRowNoSplits, 1=JustRowSplits, 2=FixedDistanceNoSplits, etc.).
        /// </summary>
        public int? WorkoutType { get; set; }
    }
}