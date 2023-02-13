using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDFSecuReader
{
    //App settings type
    public class AppSettings
    {
        public bool UseHWAcceleratedVideo { get; set; }
        public ReaderTypes PrefferedReader { get; set; }
    }
}
