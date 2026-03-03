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
        int nextStrokeId = 0;

        private ErgData currentStroke = new();
        private Concept2ForceCurveAssembler forceCurveAssembler = new();
        private bool currentStrokeUpdated = false;

        private ErgData currentStatus = new();
        private ErgData? completedStatus = null;
        private StrokeState? strokeState = null;

        public void OnGeneralStatusMessage(byte[] data)
        {
            ErgData generalStatus = Concept2DataParsing.ParseGeneralStatus(data);

            lock (dataLock)
            {
                // Clear out the force curve at the change from drive to recovery
                // This is in case we didn't get the full force curve data for the
                // stroke. If we didn't do this, the old force curve data would
                // interfere with the new force curve data.
                if (strokeState.HasValue && strokeState == StrokeState.DrivingState &&
                    generalStatus.StrokeState == StrokeState.RecoveryState)
                {
                    forceCurveAssembler.ResetForceCurve();
                }

                bool newStatus = currentStatus.ElapsedTime != generalStatus.ElapsedTime;

                if (newStatus)
                {
                    // This is a new set of status messages.
                    currentStatus = new();
                }

                currentStatus.Timestamp = generalStatus.Timestamp;
                currentStatus.ElapsedTime = generalStatus.ElapsedTime;
                currentStatus.Distance = generalStatus.Distance;
                currentStatus.WorkoutType = generalStatus.WorkoutType;
                currentStatus.WorkoutState = generalStatus.WorkoutState;
                currentStatus.StrokeState = generalStatus.StrokeState;
                currentStatus.DragFactor = generalStatus.DragFactor;

                if (!newStatus)
                {
                    // This is the second of two related status messages
                    // So the status is complete
                    completedStatus = currentStatus;
                }
            }
        }

        public void OnAdditionalStatusMessage(byte[] data)
        {
            ErgData additionalStatus = Concept2DataParsing.ParseAdditionalStatus(data);
            lock (dataLock)
            {
                bool newStatus = currentStatus.ElapsedTime != additionalStatus.ElapsedTime;
                if (newStatus)
                {
                    // This is a new set of status messages.
                    currentStatus = new();
                }

                currentStatus.Timestamp = additionalStatus.Timestamp;
                currentStatus.ElapsedTime = additionalStatus.ElapsedTime;
                currentStatus.Speed = additionalStatus.Speed;
                currentStatus.StrokeRate = additionalStatus.StrokeRate;
                currentStatus.HeartRate = additionalStatus.HeartRate;
                currentStatus.Pace = additionalStatus.Pace;
                currentStatus.AveragePace = additionalStatus.AveragePace;

                if (!newStatus)
                {
                    // This is the second of two related status messages
                    // So the status is complete
                    completedStatus = currentStatus;
                }
            }
        }

        public void OnStrokeDataMessage(byte[] data)
        {
            ErgData strokeData = Concept2DataParsing.ParseStrokeData(data);
            lock (dataLock)
            {
                bool newStroke = currentStroke.ElapsedTime != strokeData.ElapsedTime;
                if (newStroke)
                {
                    // This is a new set of stroke data.
                    currentStroke = new();
                    currentStroke.StrokeId = nextStrokeId++;
                }

                currentStroke.Timestamp = strokeData.Timestamp;
                currentStroke.ElapsedTime = strokeData.ElapsedTime;
                currentStroke.Distance = strokeData.Distance;

                currentStrokeUpdated = true;
            }
        }

        public void OnAdditionalStrokeDataMessage(byte[] data)
        {
            ErgData additionalStrokeData = Concept2DataParsing.ParseAdditionalStrokeData(data);
            lock (dataLock)
            {
                bool newStroke = currentStroke.ElapsedTime != additionalStrokeData.ElapsedTime;
                if (newStroke)
                {
                    // This is a new set of stroke data.
                    currentStroke = new();
                    currentStroke.StrokeId = nextStrokeId++;
                }

                currentStroke.Timestamp = additionalStrokeData.Timestamp;
                currentStroke.ElapsedTime = additionalStrokeData.ElapsedTime;
                currentStroke.Power = additionalStrokeData.Power;
                currentStroke.Calories = additionalStrokeData.Calories;

                currentStrokeUpdated = true;
            }
        }

        public void OnForceCurveMessage(byte[] data)
        {
            lock (dataLock)
            {
                forceCurveAssembler.HandlePowerCurveMessage(data);
                int[]? fullCurve = forceCurveAssembler.TryGetCompletedForceCurve();
                if (fullCurve != null)
                {
                    currentStroke.ForceCurve = fullCurve;
                    currentStrokeUpdated = true;
                }
            }
        }

        public ErgData? TryGetUpdatedStroke()
        {
            lock (dataLock)
            {
                if (currentStrokeUpdated)
                {
                    currentStrokeUpdated = false;
                    return currentStroke.Clone();
                }
            }

            return null;
        }

        public ErgData? TryGetCompletedStatus()
        {
            lock (dataLock)
            {
                ErgData? status = completedStatus;
                completedStatus = null;
                return status;
            }
        }
    }
}
