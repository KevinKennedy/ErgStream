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
            DoGeneralStatusParsingTestCase("19|00000000000000010000000000000000008000", "<timeStamp>,0.00,0,,,,,,,,0,,0,0");

            // workout type = 1
            DoGeneralStatusParsingTestCase("19|00000000000001010000000000000000008000", "<timeStamp>,0.00,0,,,,,,,,0,,0,1");

            // Distance 0.5 and elapsed time 0.21
            DoGeneralStatusParsingTestCase("19|15000005000001010000020000000000008000", "<timeStamp>,0.21,0.5,,,,,,,,0,,0,1");

            // Distance 17.8, elapsed time 6.18, drag factor 108
            DoGeneralStatusParsingTestCase("19|6A0200B200000101010102000000000000806C", "<timeStamp>,6.18,17.8,,,,,,,,108,,1,1");

            // Distance 55.8, elapsed time 18.59, drag factor 108
            DoGeneralStatusParsingTestCase("19|4307002E02000101010001000000000000806C", "<timeStamp>,18.59,55.8,,,,,,,,108,,1,1");
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
