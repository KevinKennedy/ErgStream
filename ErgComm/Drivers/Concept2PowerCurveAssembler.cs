using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErgComm.Drivers
{
    public class Concept2PowerCurveAssembler
    {
        private Dictionary<int, int[]> _currentPowerCurve = new();
        private int _expectedMessageCount = -1;

        public void ResetPowerCurve()
        {
            _currentPowerCurve.Clear();
        }

        public void HandlePowerCurveMessage(byte[] data)
        {
            var powerCurvePart = Concept2DataParsing.ParseForceCurveData(data);
            if (powerCurvePart.sequenceNumber == -1)
            {
                // We received some invalid data so just clear it out
                // This can happen of BLE is overloaded
                ResetPowerCurve();
                return;
            }

            if (_currentPowerCurve.ContainsKey(powerCurvePart.sequenceNumber))
            {
                // Duplicate key, assume this is a new power curve and clear existing data
                _currentPowerCurve.Clear();
            }

            _currentPowerCurve[powerCurvePart.sequenceNumber] = powerCurvePart.samples;
            _expectedMessageCount = powerCurvePart.characteristicCount;
        }

        public int[]? TryGetCompletedPowerCurve()
        {
            if (_expectedMessageCount == -1 || _currentPowerCurve.Count != _expectedMessageCount)
            {
                // Power curve is not complete
                return null;
            }

            int totalSampleCount = _currentPowerCurve.Sum(kvp => kvp.Value.Length);
            int[] fullCurve = new int[totalSampleCount];
            for (int characteristicIndex = 0, offset = 0; characteristicIndex < _expectedMessageCount; characteristicIndex++)
            {
                if (_currentPowerCurve.TryGetValue(characteristicIndex, out var samples))
                {
                    Array.Copy(samples, 0, fullCurve, offset, samples.Length);
                    offset += samples.Length;
                }
                else
                {
                    // Missing part of the power curve, this shouldn't happen but just clear out to be safe
                    _currentPowerCurve.Clear();
                    return null;
                }
            }

            _currentPowerCurve.Clear();
            return fullCurve;
        }
    }
}
