using ErgComm.Drivers;
using ErgComm.Models;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;

namespace ErgCommTests
{
    public class DataParsingTests
    {
        private readonly ITestOutputHelper _output;

        public DataParsingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GeneralStatusParsing()
        {
            DoGeneralStatusParsingTestCase("19|00000000000000010000000000000000008000", "<timeStamp>,0.00,0,,,,,,,,0,WaitingForWheelToReachMinSpeedState,0,0");

            // workout type = 1
            DoGeneralStatusParsingTestCase("19|00000000000001010000000000000000008000", "<timeStamp>,0.00,0,,,,,,,,0,WaitingForWheelToReachMinSpeedState,0,1");

            // Distance 0.5 and elapsed time 0.21
            DoGeneralStatusParsingTestCase("19|15000005000001010000020000000000008000", "<timeStamp>,0.21,0.5,,,,,,,,0,DrivingState,0,1");

            // Distance 17.8, elapsed time 6.18, drag factor 108
            DoGeneralStatusParsingTestCase("19|6A0200B200000101010102000000000000806C", "<timeStamp>,6.18,17.8,,,,,,,,108,DrivingState,1,1");

            // Distance 55.8, elapsed time 18.59, drag factor 108
            DoGeneralStatusParsingTestCase("19|4307002E02000101010001000000000000806C", "<timeStamp>,18.59,55.8,,,,,,,,108,WaitingForWheelToAccelerateState,1,1");
        }

        [Fact]
        public void AdditionalStatusParsing()
        {
            DoAdditionalStatusParsingTestCase("17|000000000000FF00000000000000000000", "<timeStamp>,0.00,,0.00,0,,,,,,,,,");

            DoAdditionalStatusParsingTestCase("17|7900005B0A00FFDA49AD49000000000000", "<timeStamp>,1.21,,2.65,0,,3:9.1,3:8.6,,,,,,");

            DoAdditionalStatusParsingTestCase("17|D00200750C16FF6C3DAD42000000000000", "<timeStamp>,7.20,,3.19,22,,2:37.2,2:50.7,,,,,,");

            DoAdditionalStatusParsingTestCase("17|430700E30C15FF643B1041000000000000", "<timeStamp>,18.59,,3.30,21,,2:32.0,2:46.6,,,,,,");
        }

        [Fact]
        public void StrokeDataParsing()
        {
            DoStrokeDataParsingTestCase("20|0000000000000000000000000000000000000000", "<timeStamp>,0.00,0,,,,,,,,,,,");

            DoStrokeDataParsingTestCase("20|59000018000055000000C8000000000000000100", "<timeStamp>,0.89,2.4,,,,,,,,,,,");

            DoStrokeDataParsingTestCase("20|DC03002C01007048C700B203CE038502250B0400", "<timeStamp>,9.88,30,,,,,,,,,,,");
        }

        [Fact]
        public void AdditionalStrokeDataParsing()
        {
            DoAdditionalStrokeDataParsingTestCase("15|000000000000000000000000000000", "<timeStamp>,0.00,,,,,,,0.0,0,,,,");

            DoAdditionalStrokeDataParsingTestCase("15|5900003400E0010100000000A41200", "<timeStamp>,0.89,,,,,,,52.0,480,,,,");

            DoAdditionalStrokeDataParsingTestCase("15|DC0300630081020400000000101700", "<timeStamp>,9.88,,,,,,,99.0,641,,,,");
        }

        [Fact]
        public void ForceCurveDataParsing()
        {
            DoForceCurveDataParsingTestCase("20|6900100010001E00290029002C00370037003C00", "6 0 [16, 16, 30, 41, 41, 44, 55, 55, 60]");
            DoForceCurveDataParsingTestCase("20|690440003A0033002E002E00270021001A000F00", "6 4 [64, 58, 51, 46, 46, 39, 33, 26, 15]");
            DoForceCurveDataParsingTestCase("8|63050F0008000200", "6 5 [15, 8, 2]");
        }

        /// <summary>
        /// Golden-path test of the ForceCurveAssembler
        /// Could probably use tests for exceptional cases
        /// </summary>
        [Fact]
        public void ForceCurveAssembler()
        {
            var assembler = new Concept2PowerCurveAssembler();
            Assert.Null(assembler.TryGetCompletedPowerCurve());
            assembler.HandlePowerCurveMessage(ParseDataString("20|6900100010001E00290029002C00370037003C00"));
            Assert.Null(assembler.TryGetCompletedPowerCurve());
            assembler.HandlePowerCurveMessage(ParseDataString("20|690146004600430048004A004A004E004E004E00"));
            Assert.Null(assembler.TryGetCompletedPowerCurve());
            assembler.HandlePowerCurveMessage(ParseDataString("20|69024E0050005200520050005000520052004E00"));
            Assert.Null(assembler.TryGetCompletedPowerCurve());
            assembler.HandlePowerCurveMessage(ParseDataString("20|69034F005100510051004E004B00470047004300"));
            Assert.Null(assembler.TryGetCompletedPowerCurve());
            assembler.HandlePowerCurveMessage(ParseDataString("20|690440003A0033002E002E00270021001A000F00"));
            Assert.Null(assembler.TryGetCompletedPowerCurve());
            assembler.HandlePowerCurveMessage(ParseDataString("8|63050F0008000200"));

            int[]? fullCurve = assembler.TryGetCompletedPowerCurve();
            Assert.NotNull(fullCurve);

            string resultString = $"[{string.Join(", ", fullCurve)}]";
            string expected = "[16, 16, 30, 41, 41, 44, 55, 55, 60, 70, 70, 67, 72, 74, 74, 78, 78, 78, 78, 80, 82, 82, 80, 80, 82, 82, 78, 79, 81, 81, 81, 78, 75, 71, 71, 67, 64, 58, 51, 46, 46, 39, 33, 26, 15, 15, 8, 2]";
            
            if (resultString != expected)
            {
                _output.WriteLine("ForceCurveAssembler test failed. If this is expected, here is the new expected string:");
                _output.WriteLine($"{resultString}");
            }
            
            Assert.Equal(expected, resultString);
        }

        private void DoGeneralStatusParsingTestCase(string hexString, string expectedCSV)
        {
            DoParsingTestCase(hexString, Concept2DataParsing.ParseGeneralStatus, expectedCSV);
        }

        private void DoAdditionalStatusParsingTestCase(string hexString, string expectedCSV)
        {
            DoParsingTestCase(hexString, Concept2DataParsing.ParseAdditionalStatus, expectedCSV);
        }

        private void DoStrokeDataParsingTestCase(string hexString, string expectedCSV)
        {
            DoParsingTestCase(hexString, Concept2DataParsing.ParseStrokeData, expectedCSV);
        }

        private void DoAdditionalStrokeDataParsingTestCase(string hexString, string expectedCSV)
        {
            DoParsingTestCase(hexString, Concept2DataParsing.ParseAdditionalStrokeData, expectedCSV);
        }

        private void DoParsingTestCase(string hexString, Func<byte[], ErgData> parseFunc, string expectedCSV, [CallerMemberName] string testFunctionName = "?????")
        {
            byte[] data = ParseDataString(hexString);
            ErgData parsedData = parseFunc(data);
            string csv = parsedData.ToCSV(includeTimestamp:false);
            if (csv != expectedCSV)
            {
                _output.WriteLine("DoParsingTestCase failed.  If this is expected, here is the new code:");
                _output.WriteLine($"{testFunctionName}(\"{hexString}\", \"{csv}\");");
            }

            Assert.Equal(expectedCSV, csv);
        }

        private void DoForceCurveDataParsingTestCase(string hexString, string expected)
        {
            byte[] data = ParseDataString(hexString);
            (int characteristicCount, int sequenceNumber, int[] samples) = Concept2DataParsing.ParseForceCurveData(data);
            string resultString = ForceCurveResultToString(characteristicCount, sequenceNumber, samples);
            if (resultString != expected)
            {
                _output.WriteLine("DoForceCurveDataParsingTestCase failed.  If this is expected, here is the new code:");
                _output.WriteLine($"DoForceCurveDataParsingTestCase(\"{hexString}\", \"{resultString}\");");
            }

            Assert.Equal(expected, resultString);
        }

        private string ForceCurveResultToString(int characteristicCount, int sequenceNumber, int[] samples)
        {
            return $"{characteristicCount} {sequenceNumber} [{string.Join(", ", samples)}]";
        }



        private static byte[] ParseDataString(string v)
        {
            string[] parts = v.Split('|');
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid data string format. Expected format: 'length|hexdata'");
            }
            byte[] data = Convert.FromHexString(parts[1]);
            if (data.Length != int.Parse(parts[0]))
            {
                throw new ArgumentException($"Data length mismatch. Expected {parts[0]} bytes but got {data.Length} bytes.");
            }
            return data;
        }
    }
}
