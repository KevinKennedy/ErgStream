using System;
using System.Collections.Generic;

namespace ErgComm.Models
{
    /// <summary>
    /// Represents real-time data from an ergometer.
    /// </summary>
    public class StrokeData : ErgComm.Interfaces.ICsvDump
    {
        /// <summary>
        /// Unique ID of the stroke. You will get multiple updates
        /// for the same stroke as more data comes in.
        /// </summary>
        public int StrokeId { get; set; } = -1;

        /// <summary>
        /// Timestamp when the data was captured. In local time for easier display, but could be converted to UTC if needed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Elapsed time in seconds.
        /// </summary>
        public double? ElapsedTime { get; set; }

        /// <summary>
        /// Distance in meters.
        /// </summary>
        public double? Distance { get; set; }

        /// <summary>
        /// Current power output in watts.
        /// </summary>
        public double? Power { get; set; }

        /// <summary>
        /// Total calories burned.
        /// </summary>
        public int? Calories { get; set; }

        /// <summary>
        /// Power curve data points (force values over time during the stroke).
        /// </summary>
        public int[]? ForceCurve { get; set; }

        public bool IsComplete()
        {
            // When we've received distance, power, and force curve data
            // we know we've received everything we're going to about this stroke
            return Distance.HasValue && Power.HasValue && ForceCurve != null;
        }

        // Helper to clone StrokeData for thread-safe callbacks
        public StrokeData Clone()
        {
            return new StrokeData
            {
                StrokeId = StrokeId,
                Timestamp = Timestamp,
                ElapsedTime = ElapsedTime,
                Distance = Distance,
                Power = Power,
                Calories = Calories,
                ForceCurve = ForceCurve, // shallow copy since this doesn't get updated (right now at least)
            };
        }

        public string GetCsvHeader()
        {
            return "Timestamp,ElapsedTime,Distance,Power,Calories,ForceCurve";
        }

        public string ToCsv(bool maskNondeterministicData = false)
        {
            string forceCurve = string.Empty;
            if (ForceCurve != null)
            {
                forceCurve = "," + ForceCurveToCSV();
            }

            // Create a CSV line with all properties, using empty string for null values
            return $"{(maskNondeterministicData ? "<timeStamp>" : Timestamp.ToString("O"))}," + // ISO 8601 format for timestamp
                   $"{ElapsedTime?.ToString("F2") ?? ""}," +
                   $"{Distance?.ToString() ?? ""}," +
                   $"{Power?.ToString("F1") ?? ""}," +
                   $"{Calories?.ToString() ?? ""}," +
                   forceCurve;
        }

        public string ForceCurveToCSV()
        {
            if (ForceCurve == null || ForceCurve.Length == 0)
            {
                return "";
            }

            return string.Join(",", ForceCurve);
        }

        override public string ToString()
        {
            return $"{Timestamp.ToString("O")}  " + // ISO 8601 format for timestamp
                   $"Elapsed: {ElapsedTime?.ToString("F2") ?? "-"}  " +
                   $"Distance: {Distance?.ToString() ?? "-"}  " +
                   $"Power: {Power?.ToString("F1") ?? "-"}  " +
                   $"Calories: {Calories?.ToString() ?? "-"}  ";

        }

    }
}