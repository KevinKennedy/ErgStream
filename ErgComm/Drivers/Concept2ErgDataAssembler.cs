using ErgComm.Models;

namespace ErgComm.Drivers
{
    /// <summary>
    /// Takes the raw BLE messages from the Concept2 rower and assembles them
    /// into complete status and stroke data objects that can be consumed by
    /// the the application.
    /// </summary>
    public class Concept2ErgDataAssembler
    {
        private object dataLock = new();

        private int nextStrokeId = 0;
        private StrokeData currentStroke = new();
        private Concept2ForceCurveAssembler forceCurveAssembler = new();
        private bool currentStrokeUpdated = false;

        private int nextStatusId = 10000;
        private ErgStatus? currentStatus = null;
        private bool currentStatusUpdated = false;
        private StrokeState? strokeState = null;

        public void OnGeneralStatusMessage(byte[] data)
        {
            ErgStatus generalStatus = Concept2DataParsing.ParseGeneralStatus(data);

            lock (dataLock)
            {
                // Clear out the force curve at the change from drive to recovery.
                // This is in case we didn't get the full force curve data for the
                // stroke. If we didn't do this, the old force curve data would
                // interfere with the new force curve data.
                if (strokeState.HasValue && strokeState == StrokeState.DrivingState &&
                    generalStatus.StrokeState == StrokeState.RecoveryState)
                {
                    forceCurveAssembler.ResetForceCurve();
                }

                if (currentStatus == null ||
                        currentStatus.ElapsedTime != generalStatus.ElapsedTime)
                {
                    // This is a new set of status messages.
                    currentStatus = new();
                    currentStatus.StatusId = nextStatusId++;
                }

                currentStatus.Timestamp = generalStatus.Timestamp;
                currentStatus.ElapsedTime = generalStatus.ElapsedTime;
                currentStatus.Distance = generalStatus.Distance;
                currentStatus.WorkoutType = generalStatus.WorkoutType;
                currentStatus.WorkoutState = generalStatus.WorkoutState;
                currentStatus.StrokeState = generalStatus.StrokeState;
                currentStatus.DragFactor = generalStatus.DragFactor;

                currentStatusUpdated = true;
            }
        }

        public void OnAdditionalStatusMessage(byte[] data)
        {
            ErgStatus additionalStatus = Concept2DataParsing.ParseAdditionalStatus(data);
            lock (dataLock)
            {
                if (currentStatus == null ||
                        currentStatus.ElapsedTime != additionalStatus.ElapsedTime)
                {
                    // This is a new set of status messages.
                    currentStatus = new();
                    currentStatus.StatusId = nextStatusId++;
                }

                currentStatus.Timestamp = additionalStatus.Timestamp;
                currentStatus.ElapsedTime = additionalStatus.ElapsedTime;
                currentStatus.Speed = additionalStatus.Speed;
                currentStatus.StrokeRate = additionalStatus.StrokeRate;
                currentStatus.HeartRate = additionalStatus.HeartRate;
                currentStatus.Pace = additionalStatus.Pace;
                currentStatus.AveragePace = additionalStatus.AveragePace;

                currentStatusUpdated = true;
            }
        }

        public void OnStrokeDataMessage(byte[] data)
        {
            StrokeData strokeData = Concept2DataParsing.ParseStrokeData(data);
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
            StrokeData additionalStrokeData = Concept2DataParsing.ParseAdditionalStrokeData(data);
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

        public StrokeData? TryGetUpdatedStroke()
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

        public ErgStatus? TryGetUpdatedStatus()
        {
            lock (dataLock)
            {
                if (currentStatusUpdated && currentStatus != null)
                {
                    currentStatusUpdated = false;
                    return currentStatus.Clone();
                }
            }

            return null;
        }
    }
}
