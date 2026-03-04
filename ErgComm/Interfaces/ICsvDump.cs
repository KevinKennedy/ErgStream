using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ErgComm.Interfaces
{
    public interface ICsvDump
    {
        string GetCsvHeader();
        string ToCsv(bool maskNondeterministicData = false);
    }
}
