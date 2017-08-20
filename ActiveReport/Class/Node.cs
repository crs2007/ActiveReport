using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ActiveReport.Class
{
    public class Node
    {
        public string Server { get; set; }
        public string Property { get; set; }
        public string Value { get; set; }
    }

    public class VolumeInfo
    {
        public string Drive { get; set; }
        public string Total_Size { get; set; }
        public string Free_Space { get; set; }
        public string Label { get; set; }
		
	}

	public class EventViewer
	{
		public string LogType { get; set; }
		public int EventCount { get; set; }
		public string EventMsg { get; set; }

	}

	public class SQLNodeOut
    {
        public string NodeName { get; set; }
    }

    public class InvalidLogin
    {
        public string Text { get; set; }
    }

    public class SQLLogin
    {
        public string ServerName { get; set; }
        public string name { get; set; }
        public string sid { get; set; }
    }

    public class SQLBackup
    {
            
        public string ServerName { get; set; }
        public string DatabaseName { get; set; }
        public string LastBackUpTime { get; set; }
        public string Type { get; set; }
        public string physical_device_name { get; set; }
    }
    
    public class SQLBackupSingle
    {
        
        public string DatabaseName { get; set; }
        public string LastBackUpTime { get; set; }
        public string Type { get; set; }
        public string physical_device_name { get; set; }
    }


    public class SQLErrorLog
    {
        public DateTime LogDate { get; set; }
        public string ProcessesInfo { get; set; }
        public string Message { get; set; }
    }

    public class SQLErrorDetailLog
    {

        public DateTime LogDate { get; set; }
        public string ProcessesInfo { get; set; }
        public string Error { get; set; }
        public string Severity { get; set; }
        public string State { get; set; }
    }

    public class SQLErrorFullDetailLog
    {
        public string Message { get; set; }
        public string UserName { get; set; }
        public string Error { get; set; }
        public string Severity { get; set; }
        public string State { get; set; }
        public string ProcessesInfo { get; set; }
        public int Counts { get; set; }
        public string Databases { get; set; }
        public string OriginalMessage { get; set; }
    }
}
