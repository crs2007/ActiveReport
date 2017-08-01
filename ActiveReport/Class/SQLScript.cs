using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveReport.Class
{
    public class SQLScript
    {
        public string FileName { set; get; }
        public string Content { set; get; }


        public SQLScript(string fileName, string content)
        {
            FileName = fileName;
            Content = content;
        }
		
    }
}
