using ErgComm.Drivers;
using ErgComm.Models;
using Xunit.Abstractions;

namespace ErgCommTests
{
    public class ErgDataAssemblerTest
    {
        private readonly ITestOutputHelper _output;

        public ErgDataAssemblerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void LogDataTest()
        {
            List<StrokeData> strokeUpdates = new();
            Dictionary<int, StrokeData> completeStrokes = new();
            List<ErgStatus> statusUpdates = new();
            Dictionary<int, ErgStatus> completedStatuses = new();

            Concept2ErgDataAssembler ergDataAssembler = new();

            foreach ((LogDataType dataType, string hexString) in ErgDataAssemblerTestData.LogData)
            {
                byte[] data = DataParsingTests.ParseDataString(hexString);

                switch (dataType)
                {
                    case LogDataType.GeneralStatus:
                        ergDataAssembler.OnGeneralStatusMessage(data);
                        break;
                    case LogDataType.AdditionalStatus:
                        ergDataAssembler.OnAdditionalStatusMessage(data);
                        break;
                    case LogDataType.StrokeData:
                        ergDataAssembler.OnStrokeDataMessage(data);
                        break;
                    case LogDataType.AdditionalStrokeData:
                        ergDataAssembler.OnAdditionalStrokeDataMessage(data);
                        break;
                    case LogDataType.ForceCurveData:
                        ergDataAssembler.OnForceCurveMessage(data);
                        break;
                }

                StrokeData? stroke = ergDataAssembler.TryGetUpdatedStroke();
                if (stroke != null)
                {
                    strokeUpdates.Add(stroke);
                    completeStrokes[stroke.StrokeId] = stroke;
                }

                ErgStatus? status = ergDataAssembler.TryGetUpdatedStatus();
                if (status != null)
                {
                    statusUpdates.Add(status);
                    completedStatuses[status.StatusId] = status;
                }
            }

            // TODO: maybe verify some other things...

            Assert.Equal(152, strokeUpdates.Count);
            Assert.Equal(77, completeStrokes.Count);
            Assert.Equal(40, completeStrokes.Values.Count(i => i.ForceCurve == null));

            Assert.Equal(220, statusUpdates.Count);
            Assert.Equal(82, completedStatuses.Count);
        }
    }
}
