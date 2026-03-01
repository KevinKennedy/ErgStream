using ErgComm.Drivers;
using ErgComm.Models;
using Xunit.Abstractions;

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
            DoGeneralStatusParsingTestCase("19|00000000000000010000000000000000008000", "<timeStamp>,0.00,0,,,,,,0,,0,0");

            // workout type = 1
            DoGeneralStatusParsingTestCase("19|00000000000001010000000000000000008000", "<timeStamp>,0.00,0,,,,,,0,,0,1");

            // Distance 0.5 and elapsed time 0.21
            DoGeneralStatusParsingTestCase("19|15000005000001010000020000000000008000", "<timeStamp>,0.21,0.5,,,,,,0,,0,1");

            // Distance 17.8, elapsed time 6.18, drag factor 108
            DoGeneralStatusParsingTestCase("19|6A0200B200000101010102000000000000806C", "<timeStamp>,6.18,17.8,,,,,,108,,1,1");
            
            // Distance 55.8, elapsed time 18.59, drag factor 108
            DoGeneralStatusParsingTestCase("19|4307002E02000101010001000000000000806C", "<timeStamp>,18.59,55.8,,,,,,108,,1,1");

        }

        private void DoGeneralStatusParsingTestCase(string hexString, string expectedCSV)
        {
            byte[] data = ParseDataString(hexString);
            ErgData ergData = Concept2DataParsing.ParseGeneralStatus(data);
            string csv = ergData.ToCSV(includeTimestamp:false);

            if (csv != expectedCSV)
            {
                _output.WriteLine("DoGeneralStatusParsingTestCase failed.  If this is expected, here is the new code:");
                _output.WriteLine($"DoGeneralStatusParsingTestCase(\"{hexString}\", \"{csv}\");");
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
