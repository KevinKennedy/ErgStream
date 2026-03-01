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
            if (data == null || data.Length < 19)
                return new ErgData();

            int offset = 0;

            ErgData e = new ErgData();
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
            if (data == null || data.Length < 7)
                return new ErgData();

            return new ErgData
            {
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0,
                Calories = BitConverter.ToUInt16(data, 4),
                WorkoutType = data[6]
            };
        }

        // Parse Stroke Data characteristic (0x0035) - Stroke-End Events
        public static ErgData ParseStrokeData(byte[] data)
        {
            if (data == null || data.Length < 19)
                return new ErgData();

            return new ErgData
            {
                Timestamp = DateTime.Now,
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0,
                Distance = BitConverter.ToUInt32(data, 4) / 10.0,
                Power = BitConverter.ToUInt16(data, 8),
                StrokeRate = data[10],
                Calories = BitConverter.ToUInt16(data, 11),
                Pace = BitConverter.ToUInt16(data, 15) / 10.0,
            };
        }

        // Parse Additional Stroke Data characteristic (0x0036)
        public static ErgData ParseAdditionalStrokeData(byte[] data)
        {
            if (data == null || data.Length < 17)
                return new ErgData();

            return new ErgData
            {
                ElapsedTime = BitConverter.ToUInt32(data, 0) / 100.0,
                Power = BitConverter.ToUInt16(data, 4),
                DragFactor = data[12]
            };
        }

        // Parse Force Curve Data characteristic (0x0038)
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

        private static uint ParseByte(byte[] data, ref int offset)
        {
            uint value = data[offset];
            offset += 1;
            return value;
        }
    }
}
