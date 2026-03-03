using ErgComm.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErgComm.Drivers
{
    /// <summary>
    /// Takes the raw BLE messages from the Concept2 rower and assembles them
    /// into complete stroke data objects that can be consumed by the the application
    /// </summary>
    public class Concept2StrokeAssembler
    {
        private object dataLock = new();
        private ErgData currentStroke = new();
        private Concept2ForceCurveAssembler forceCurveAssembler = new();
        private ErgData? completedStroke = null;

        public void OnGeneralStatusMessage(byte[] data)
        {
            ErgData generalStatus = Concept2DataParsing.ParseGeneralStatus(data);

            lock (dataLock)
            {
                StrokeState? previousStrokeState = currentStroke.StrokeState;
                currentStroke.Timestamp = generalStatus.Timestamp;
                currentStroke.ElapsedTime = generalStatus.ElapsedTime;
                currentStroke.Distance = generalStatus.Distance;
                currentStroke.WorkoutType = generalStatus.WorkoutType;
                currentStroke.WorkoutState = generalStatus.WorkoutState;
                currentStroke.StrokeState = generalStatus.StrokeState;
                currentStroke.DragFactor = generalStatus.DragFactor;

                if(previousStrokeState == StrokeState.DrivingState &&
                    currentStroke.StrokeState == StrokeState.RecoveryState)
                {
                    // The stroke is complete, move it to the completed stroke and start a new one
                    completedStroke = currentStroke;
                    currentStroke = new();
                    forceCurveAssembler.ResetForceCurve();
                }
            }
        }

        public void OnAdditionalStatusMessage(byte[] data)
        {
            ErgData additionalStatus = Concept2DataParsing.ParseAdditionalStatus(data);
            lock (dataLock)
            {
                currentStroke.Timestamp = additionalStatus.Timestamp;
                currentStroke.ElapsedTime = additionalStatus.ElapsedTime;
                currentStroke.Speed = additionalStatus.Speed;
                currentStroke.StrokeRate = additionalStatus.StrokeRate;
                currentStroke.HeartRate = additionalStatus.HeartRate;
                currentStroke.Pace = additionalStatus.Pace;
                currentStroke.AveragePace = additionalStatus.AveragePace;
            }
        }

        public void OnStrokeDataMessage(byte[] data)
        {
            ErgData strokeData = Concept2DataParsing.ParseStrokeData(data);
            lock (dataLock)
            {
                currentStroke.Timestamp = strokeData.Timestamp;
                currentStroke.ElapsedTime = strokeData.ElapsedTime;
                currentStroke.Distance = strokeData.Distance;
            }
        }

        public void OnAdditionalStrokeDataMessage(byte[] data)
        {
            ErgData additionalStrokeData = Concept2DataParsing.ParseAdditionalStrokeData(data);
            lock (dataLock)
            {
                currentStroke.Timestamp = additionalStrokeData.Timestamp;
                currentStroke.ElapsedTime = additionalStrokeData.ElapsedTime;
                currentStroke.Power = additionalStrokeData.Power;
                currentStroke.Calories = additionalStrokeData.Calories;
            }
        }

        public void OnForceCurveMessage(byte[] data)
        {
            lock(dataLock)
            {
                forceCurveAssembler.HandlePowerCurveMessage(data);
                int[]? fullCurve = forceCurveAssembler.TryGetCompletedForceCurve();
                if (fullCurve != null)
                {
                    currentStroke.ForceCurve = fullCurve;
                }
            }
        }

        public ErgData? TryGetCompletedStroke()
        {
            lock (dataLock)
            {
                ErgData? stroke = completedStroke;
                completedStroke = null;
                return stroke;
            }
        }
    }
}
