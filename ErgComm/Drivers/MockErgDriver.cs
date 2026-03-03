using ErgComm.Interfaces;
using ErgComm.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ErgComm.Drivers
{
    /// <summary>
    /// Mock ergometer device for testing and development.
    /// </summary>
    public class MockErgDriver : IErgDriver
    {
        private const string MockErgId = "MOCK-ERG-001";
        private const string MockErgName = "Mock Erg (Test Data)";
        private readonly Random _random = new();

        public Task StartDiscoveryAsync(Action<List<ErgInfo>> ergListCallback, CancellationToken cancellationToken)
        {
            // Immediately return the mock erg
            var mockErg = new ErgInfo
            {
                Id = MockErgId,
                Name = MockErgName,
                SignalStrength = -45, // Excellent signal
                ErgType = ErgType.MockErg,
                IsMockErg = true
            };

            ergListCallback(new List<ErgInfo> { mockErg });
            return Task.CompletedTask;
        }

        public async Task ConnectAndStreamAsync(string ergId, Action<ErgData> dataCallback, CancellationToken cancellationToken)
        {
            if (ergId != MockErgId)
                throw new ArgumentException($"Invalid mock erg ID: {ergId}");

            var startTime = DateTime.UtcNow;
            var sessionStartTime = DateTime.UtcNow;
            double distance = 0;
            //int strokeCount = 0;
            //int calories = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = (DateTime.UtcNow - sessionStartTime).TotalSeconds;
                
                // Simulate realistic rowing metrics
                var strokeRate = 20 + _random.Next(-3, 4); // 17-23 spm
                var power = 180 + _random.Next(-30, 30); // 150-210 watts
                var pace = CalculatePaceFromPower(power);
                
                // Update distance based on pace
                var distanceIncrement = (500.0 / pace) * 0.1; // Distance covered in 100ms at current pace
                distance += distanceIncrement;

                // Simulate power curve (force data points during stroke)
                var forceCurve = GenerateMockForceCurve(power);

                // Simulate stroke state cycling
                var strokeState = ((StrokeState)((elapsed * 2) % 5)); // Cycle through states

                var data = new ErgData
                {
                    Timestamp = DateTime.UtcNow,
                    ElapsedTime = elapsed,
                    Distance = distance,
                    StrokeRate = strokeRate,
                    HeartRate = 140 + _random.Next(-10, 10), // 130-150 bpm
                    Pace = pace,
                    Power = power,
                    Calories = (int)(elapsed * 10 / 60), // ~10 cal/min
                    DragFactor = 110 + _random.Next(-5, 5), // Typical drag factor
                    StrokeState = strokeState,
                    ForceCurve = forceCurve,
                    WorkoutState = 1, // WorkoutRowState
                    WorkoutType = 0 // JustRowNoSplits
                };

                dataCallback(data);

                await Task.Delay(1000, cancellationToken); // Update every 1 second
            }
        }

        private double CalculatePaceFromPower(double watts)
        {
            // Concept2 pace formula: pace = 500 / (2.8 * watts^(1/3))
            // Simplified approximation
            return 500.0 / (2.8 * Math.Pow(watts, 1.0 / 3.0));
        }

        private int[] GenerateMockForceCurve(double avgPower)
        {
            // Generate realistic power curve with ~16 data points
            var curve = new List<int>();
            for (int i = 0; i < 16; i++)
            {
                // Bell curve shape for drive phase
                double normalized = i / 16.0;
                double bellCurve = Math.Exp(-Math.Pow((normalized - 0.4) * 4, 2));
                int force = (int)(avgPower * bellCurve * 2.5 + _random.Next(-10, 10));
                curve.Add(Math.Max(0, force));
            }
            return curve.ToArray();
        }
    }
}