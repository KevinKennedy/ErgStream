using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErgComm.Drivers
{
    public class Concept2ForceCurveAssembler
    {
        private Dictionary<int, int[]> _currentForceCurve = new();
        private int _expectedMessageCount = -1;

        public void ResetForceCurve()
        {
            _currentForceCurve.Clear();
        }

        public void HandlePowerCurveMessage(byte[] data)
        {
            var forceCurvePart = Concept2DataParsing.ParseForceCurveData(data);
            if (forceCurvePart.sequenceNumber == -1)
            {
                // We received some invalid data so just clear it out
                // This can happen of BLE is overloaded
                ResetForceCurve();
                return;
            }

            if (_currentForceCurve.ContainsKey(forceCurvePart.sequenceNumber))
            {
                // Duplicate key, assume this is a new force curve and clear existing data
                _currentForceCurve.Clear();
            }

            _currentForceCurve[forceCurvePart.sequenceNumber] = forceCurvePart.samples;
            _expectedMessageCount = forceCurvePart.characteristicCount;
        }

        public int[]? TryGetCompletedForceCurve()
        {
            if (_expectedMessageCount == -1 || _currentForceCurve.Count != _expectedMessageCount)
            {
                // Power curve is not complete
                return null;
            }

            int totalSampleCount = _currentForceCurve.Sum(kvp => kvp.Value.Length);
            int[] fullCurve = new int[totalSampleCount];
            for (int characteristicIndex = 0, offset = 0; characteristicIndex < _expectedMessageCount; characteristicIndex++)
            {
                if (_currentForceCurve.TryGetValue(characteristicIndex, out var samples))
                {
                    Array.Copy(samples, 0, fullCurve, offset, samples.Length);
                    offset += samples.Length;
                }
                else
                {
                    // Missing part of the power curve, this shouldn't happen but just clear out to be safe
                    _currentForceCurve.Clear();
                    return null;
                }
            }

            _currentForceCurve.Clear();
            return fullCurve;
        }
    }
}
