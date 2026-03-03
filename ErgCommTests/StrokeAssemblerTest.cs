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
            List<ErgData> completedStrokes = new List<ErgData>();
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

                ErgData? stroke = strokeAssembler.TryGetCompletedStroke();
                if (stroke != null)
                {
                    completedStrokes.Add(stroke);
                }
            }

            // TODO: maybe verify some other things...
            Assert.Equal(17, completedStrokes.Count);
        }
    }
}
