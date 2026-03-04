using ErgComm.Interfaces;
using System;

namespace ErgComm.Models
{
    public enum StrokeState
    {
        WaitingForWheelToReachMinSpeedState = 0,
        WaitingForWheelToAccelerateState = 1,
        DrivingState = 2,
        DwellingAfterDriveState = 3,
        RecoveryState = 4
    }

    public class ErgStatus : ICsvDump
    {
        /// <summary>
        /// Unique ID of the status. You will get multiple updates
        /// for the same status as more data comes in.
        /// </summary>
        public int StatusId { get; set; } = -1;

        /// <summary>
        /// Timestamp when this data was last updated. In local time for easier display, but could be converted to UTC if needed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Elapsed time in seconds.
        /// From the ergometer
        /// </summary>
        public double? ElapsedTime { get; set; }

        /// <summary>
        /// Distance in meters.
        /// </summary>
        public double? Distance { get; set; }

        /// <summary>
        /// Workout type (0=JustRowNoSplits, 1=JustRowSplits, 2=FixedDistanceNoSplits, etc.).
        /// </summary>
        public int? WorkoutType { get; set; }

        /// <summary>
        /// Workout state (0=WaitingToBegin, 1=WorkoutRowState, 2=CountDownPauseState, 
        /// 3=IntervalRestState, 4=WorkoutFinishedState, 5=TerminateState).
        /// </summary>
        public int? WorkoutState { get; set; }

        /// <summary>
        /// Stroke state (0=WaitingForWheelToReachMinSpeedState, 1=WaitingForWheelToAccelerateState, 
        /// 2=DrivingState, 3=DwellingAfterDriveState, 4=RecoveryState).
        /// </summary>
        public StrokeState? StrokeState { get; set; }

        /// <summary>
        /// Current drag factor (0-255).
        /// </summary>
        public int? DragFactor { get; set; }

        /// <summary>
        /// Speed in m/sec
        /// </summary>
        public double? Speed { get; set; }

        /// <summary>
        /// Current stroke rate (strokes per minute for rower).
        /// </summary>
        public int? StrokeRate { get; set; }

        /// <summary>
        /// Heart rate in beats per minute (if available).
        /// </summary>
        public int? HeartRate { get; set; }

        /// <summary>
        /// Current pace in seconds per 500m.
        /// </summary>
        public double? Pace { get; set; }

        /// <summary>
        /// Current pace in seconds per 500m.
        /// </summary>
        public double? AveragePace { get; set; }

        public bool IsComplete()
        {
            // Speed and Distance come from different messages
            return Distance.HasValue && Speed.HasValue;
        }

        public ErgStatus Clone()
        {
            return new ErgStatus
            {
                StatusId = StatusId,
                Timestamp = Timestamp,
                ElapsedTime = ElapsedTime,
                Distance = Distance,
                WorkoutType = WorkoutType,
                WorkoutState = WorkoutState,
                StrokeState = StrokeState,
                DragFactor = DragFactor,
                Speed = Speed,
                StrokeRate = StrokeRate,
                HeartRate = HeartRate,
                Pace = Pace,
                AveragePace = AveragePace,
            };
        }

        public string GetCsvHeader()
        {
            return "Timestamp,ElapsedTime,Distance,WorkoutType,WorkoutState,StrokeState,DragFactor,Speed,StrokeRate,HeartRate,Pace,AveragePace";
        }

        public string ToCsv(bool maskNondeterministicData = false)
        {
            // Create a CSV line with all properties, using empty string for null values
            return $"{(maskNondeterministicData ? "<timeStamp>" : Timestamp.ToString("O"))}," + // ISO 8601 format for timestamp
                   $"{ElapsedTime?.ToString("F2") ?? ""}," +
                   $"{Distance?.ToString() ?? ""}," +
                   $"{WorkoutType?.ToString() ?? ""}" +
                   $"{WorkoutState?.ToString() ?? ""}," +
                   $"{StrokeState?.ToString() ?? ""}," +
                   $"{DragFactor?.ToString() ?? ""}," +
                   $"{Speed?.ToString("F2") ?? ""}," +
                   $"{StrokeRate?.ToString() ?? ""}," +
                   $"{HeartRate?.ToString() ?? ""}," +
                   $"{FormatPace(Pace)}," +
                   $"{FormatPace(AveragePace)},";
        }

        /// <summary>
        /// Format pace as mm:ss.t per 500m
        /// </summary>
        /// <param name="paceSeconds"></param>
        /// <returns></returns>
        private static string FormatPace(double? paceSeconds)
        {
            if (!paceSeconds.HasValue || paceSeconds <= 0 || double.IsInfinity(paceSeconds.Value) || double.IsNaN(paceSeconds.Value))
                return "";

            int minutes = (int)(paceSeconds.Value / 60);
            double seconds = paceSeconds.Value % 60;
            return $"{minutes:D1}:{seconds:F1}";
        }

        override public string ToString()
        {
            return $"{Timestamp.ToString("O")}  " + // ISO 8601 format for timestamp
                   $"Elapsed: {ElapsedTime?.ToString("F2") ?? "-"}  " +
                   $"Distance: {Distance?.ToString() ?? "-"}  " +
                   $"WorkoutType: {WorkoutType?.ToString() ?? "-"}  " +
                   $"WorkoutState: {WorkoutState?.ToString() ?? "-"}  " +
                   $"StrokeState: {StrokeState?.ToString() ?? "-"}  " +
                   $"DragFactor: {DragFactor?.ToString() ?? "-"}  " +
                   $"Speed: {Speed?.ToString("F2") ?? "-"}  " +
                   $"StrokeRate: {StrokeRate?.ToString() ?? "-"}  " +
                   $"HeartRate: {HeartRate?.ToString() ?? "-"}  " +
                   $"Pace: {FormatPace(Pace)}  " +
                   $"AveragePace: {FormatPace(AveragePace)}";
        }
    }
}
