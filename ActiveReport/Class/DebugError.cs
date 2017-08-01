using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ActiveReport.Class
{
    public class DebugError
    {
        public string Subject { get; set; }
        public string Error { get; set; }
        public int Duration { get; set; }
        public string ServerName { get; set; }

    }

    public class AppError
    {

        public Exception Error { get; set; }
        public string ServerName { get; set; }
        public bool PrintOnScreen { get; set; }

    }
}
