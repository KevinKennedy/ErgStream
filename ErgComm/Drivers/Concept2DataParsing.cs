using ErgComm.Models;
using System;
using System.Collections.Generic;

namespace ErgComm.Drivers
{
    public static class Concept2DataParsing
    {

        // Parse Rowing General Status characteristic (0x0031)
        public static ErgData ParseGeneralStatus(byte[] data)
        {
            if (data == null || data.Length != 19)
            {
                return new();
            }

            int offset = 0;
            ErgData e = new();
            e.Timestamp = DateTime.Now;
            e.ElapsedTime = ParseInt24(data, ref offset) / 100.0; // centiseconds to seconds
            e.Distance = ParseInt24(data, ref offset) / 10.0; // decimeters to meters
            e.WorkoutType = (int) ParseByte(data, ref offset);
            offset += 1; // Skip interval type (1 byte)
            e.WorkoutState = (int)ParseByte(data, ref offset);
            offset += 1; // Skip rowing state (1 byte)
            offset += 1; // Skip stroke state (1 byte)
            offset += 3; // Skip work distance (3 bytes)
            offset += 3; // Skip work duration (3 bytes)
            offset += 1; // Skip work duration type (1 byte)
            e.DragFactor = (int) ParseByte(data, ref offset);
            return e;
        }

        // Parse Additional Status characteristic (0x0032)
        public static ErgData ParseAdditionalStatus(byte[] data)
        {
            if (data == null || data.Length != 17)
            {
                return new();
            }

            int offset = 0;
            ErgData e = new();
            e.Timestamp = DateTime.Now;
            e.ElapsedTime = ParseInt24(data, ref offset) / 100.0; // centiseconds to seconds
            e.Speed = ParseInt16(data, ref offset) / 1000.0; // millimeters per second to meters per second
            e.StrokeRate = (int) ParseByte(data, ref offset);
            e.HeartRate = (int) ParseByte(data, ref offset);
            if (e.HeartRate == 255)
            {
                // HeartRate of 255 is defined as invalid
                e.HeartRate = null;
            }
            
            e.Pace = ParseInt16(data, ref offset) / 100.0; // centiseconds per 500m to seconds per 500m
            e.AveragePace = ParseInt16(data, ref offset) / 100.0; // centiseconds per 500m to seconds per 500m
            offset += 2; // Skip rest distance (2 bytes)
            offset += 3; // Skip rest time (3 bytes)
            offset += 1; // Skip erg machine type (1 byte)
            return e;
        }

        // Parse Stroke Data characteristic (0x0035) - Stroke-End Events
        public static ErgData ParseStrokeData(byte[] data)
        {
            if (data == null || data.Length != 20)
            {
                return new();
            }

            int offset = 0;
            ErgData e = new();
            e.Timestamp = DateTime.Now;
            e.ElapsedTime = ParseInt24(data, ref offset) / 100.0; // centiseconds to seconds
            e.Distance = ParseInt24(data, ref offset) / 10.0; // decimeters to meters
            offset += 1; // Skip drive length (1 byte) (0.01 meter units, max = 2.55 meters)
            offset += 1; // Skip drive time (1 byte) (0.01 second units, max = 2.55 seconds)
            offset += 2; // Skip stroke recovery time (2 bytes)  (0.01 sec, max = 655.35 sec)
            offset += 2; // Skip stroke distance (2 bytes) (0.01 meter units, max = 655.35 meters)
            offset += 2; // Skip peak drive force (2 bytes) (0.1 lbs of force, max=6553.5m)
            offset += 2; // Skip average drive force (2 bytes) (0.1 lbs of force, max=6553.5m)
            offset += 2; // Skip work per stroke (2 bytes) (0.1 joule units, max=6553.5 joules)
            offset += 2; // Skip stroke count (2 bytes) (max=65535 strokes)
            return e;
        }

        // Parse Additional Stroke Data characteristic (0x0036)
        public static ErgData ParseAdditionalStrokeData(byte[] data)
        {
            if (data == null || data.Length != 15)
            {
                return new();
            }

            int offset = 0;
            ErgData e = new();
            e.Timestamp = DateTime.Now;
            e.ElapsedTime = ParseInt24(data, ref offset) / 100.0; // centiseconds to seconds
            e.Power = ParseInt16(data, ref offset); // watts
            e.Calories = (int) ParseInt16(data, ref offset); // calories
            offset += 2; // Skip stroke count (2 bytes) (max=65535 strokes)
            offset += 3; // Skip Projected Work Time (3 bytes) (in seconds)
            offset += 3; // Skip Projected Work Distance (3 bytes) (in meters)
            return e;
        }

        // Parse Force Curve Data characteristic (0x003D)
        public static List<int> ParseForceCurveData(byte[] data)
        {
            if (data == null || data.Length < 4)
                return new List<int>();

            var powerCurve = new List<int>();
            int dataPoints = BitConverter.ToUInt16(data, 0);

            for (int i = 0; i < dataPoints && (2 + i * 2) < data.Length; i++)
            {
                int force = BitConverter.ToUInt16(data, 2 + i * 2);
                powerCurve.Add(force);
            }

            return powerCurve;
        }

        private static uint ParseInt24(byte[] data, ref int offset)
        {
            uint value = (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16));
            offset += 3;
            return value;
        }

        private static uint ParseInt16(byte[] data, ref int offset)
        {
            uint value = (uint)(data[offset] | (data[offset + 1] << 8));
            offset += 2;
            return value;
        }

        private static uint ParseByte(byte[] data, ref int offset)
        {
            uint value = data[offset];
            offset += 1;
            return value;
        }
    }
}
