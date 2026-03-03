using ErgComm.Drivers;
using ErgComm.Models;
using Xunit.Abstractions;

namespace ErgCommTests
{
    public class StrokeAssemblerTest
    {
        private readonly ITestOutputHelper _output;

        public StrokeAssemblerTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void LogDataTest()
        {
            List<ErgData> strokeUpdates = new List<ErgData>();
            Dictionary<int, ErgData> completeStrokes = new Dictionary<int, ErgData>();
            List<ErgData> completedStatuses = new List<ErgData>();
            Concept2StrokeAssembler strokeAssembler = new Concept2StrokeAssembler();

            foreach ((LogDataType dataType, string hexString) in StrokeAssemblerTestData.LogData)
            {
                byte[] data = DataParsingTests.ParseDataString(hexString);

                switch (dataType)
                {
                    case LogDataType.GeneralStatus:
                        strokeAssembler.OnGeneralStatusMessage(data);
                        break;
                    case LogDataType.AdditionalStatus:
                        strokeAssembler.OnAdditionalStatusMessage(data);
                        break;
                    case LogDataType.StrokeData:
                        strokeAssembler.OnStrokeDataMessage(data);
                        break;
                    case LogDataType.AdditionalStrokeData:
                        strokeAssembler.OnAdditionalStrokeDataMessage(data);
                        break;
                    case LogDataType.ForceCurveData:
                        strokeAssembler.OnForceCurveMessage(data);
                        break;
                }

                ErgData? stroke = strokeAssembler.TryGetUpdatedStroke();
                if (stroke != null)
                {
                    strokeUpdates.Add(stroke);
                    completeStrokes[stroke.StrokeId] = stroke;
                }

                ErgData? status = strokeAssembler.TryGetCompletedStatus();
                if (status != null)
                {
                    completedStatuses.Add(status);
                }
            }

            // TODO: maybe verify some other things...

            Assert.Equal(152, strokeUpdates.Count);
            Assert.Equal(77, completeStrokes.Count);
            Assert.Equal(40, completeStrokes.Values.Count(i => i.ForceCurve == null));

            Assert.Equal(138, completedStatuses.Count);
        }
    }
}
