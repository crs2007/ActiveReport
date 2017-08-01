using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Diagnostics.Eventing.Reader;

namespace ActiveReport.Class
{
    public class EventLog
    {
        private DataTable outputTable = new DataTable();

        public EventLog()
        {
            outputTable.Columns.Add("EventID", typeof(int));
            outputTable.Columns.Add("EntryType", typeof(string));
            outputTable.Columns.Add("TimeGenerated", typeof(DateTime));
            outputTable.Columns.Add("Source", typeof(string));
            outputTable.Columns.Add("Message", typeof(string));
        }

        private static List<string> FindSourceNamesFromLog(string logName)
        {
            List<string> sourceNamesList = new List<string>();
            int listCapacity = 0;
            // Get the registry key for the specific log.
            RegistryKey keyLog = Registry.LocalMachine.OpenSubKey
                (@"SYSTEM\CurrentControlSet\Services\Eventlog\" + logName);
            if (keyLog != null && keyLog.SubKeyCount > 0)
            {
                // Get the sources from the log key.
                string[] sourceNames = keyLog.GetSubKeyNames();

                foreach (string subKey in sourceNames)
                {
                    if (subKey.Contains("SQL") || subKey.Contains("FailoverClustering") || subKey.Contains("Report Server"))
                    {
                        // Add subKey of the sources into the list.
                        sourceNamesList.Add(subKey);
                        listCapacity++;
                    }
                }

                // Set capacity for the list.
                sourceNamesList.Capacity = listCapacity;
            }

            // Return the list.
            return sourceNamesList;
        }

        public DataTable GetEventRecords(DateTime afterTimestamp, DateTime beforeTimestamp, string level, string logName, string providerName)
        {
            //Critical	Value: 1. Indicates logs for a critical alert.
            //Error	Value: 2. Indicates logs for an error.
            //Information	Value: 4. Indicates logs for an informational message.
            //Undefined	Value: 0. Indicates logs at all levels.
            //Verbose	Value: 5. Indicates logs at all levels.
            //Warning	Value: 3. Indicates logs for a warning.
            string errorLevel;
            string queryString = string.Empty;

            //LogLevel 
            int levelAsInt = 99;
            bool isNumeric = int.TryParse(level, out levelAsInt);
            if (String.IsNullOrEmpty(level))
            {
                errorLevel = "(Level=1  or Level=2 or Level=3)";
            }
            else if (levelAsInt >= 0 && levelAsInt <= 5)
            {
                errorLevel = "(Level = {0})";
                errorLevel = String.Format(errorLevel,
                    levelAsInt);
            }
            else
            {
                return outputTable;
            }

            if (String.IsNullOrEmpty(providerName))
            {
                List<string> SourceNames = FindSourceNamesFromLog(logName);
                if (SourceNames.Capacity == 0)
                    return outputTable;
                else if (SourceNames.Capacity > 22)
                {
                    int pos = 0;
                    int max = 20;
                    for (int i = 0; i <= SourceNames.Capacity / 20; i++)
                    {
                        if ((i + 1) * max > SourceNames.Capacity)
                            max = (SourceNames.Capacity - ((i) * max));
                        queryString = "*[System[Provider[";
                        for (int j = 0; j < max; j++)
                        {
                            if (j == 0)
                                queryString += "@Name='" + SourceNames[pos] + "'";
                            else
                                queryString += " or @Name='" + SourceNames[pos] + "'";
                            pos++;
                        }
                        queryString += "] "
                        + "and " + errorLevel + " "
                        + "and TimeCreated[@SystemTime>='{0}' and @SystemTime<='{1}']]]";
                        queryString = String.Format(queryString,
                        afterTimestamp.ToUniversalTime().ToString("o"),
                        beforeTimestamp.ToUniversalTime().ToString("o"));
                        AddEventRecord(logName, queryString);
                    }
                }
                else
                {
                    queryString = "*[System[Provider[";
                    int first = 0;
                    foreach (string value in SourceNames)
                    {
                        if (first == 0)
                            queryString += "@Name='" + value + "'";
                        else
                            queryString += " or @Name='" + value + "'";
                        first++;
                    }
                    queryString += "] "
                    + "and " + errorLevel + " "
                    + "and TimeCreated[@SystemTime>='{0}' and @SystemTime<='{1}']]]";
                    queryString = String.Format(queryString,
                    afterTimestamp.ToUniversalTime().ToString("o"),
                    beforeTimestamp.ToUniversalTime().ToString("o"));
                    AddEventRecord(logName, queryString);
                }
            }
            else
            {
                queryString = String.Format(
                    "*[System[Provider[@Name = '{0}'] and " + errorLevel +
                    " and TimeCreated[@SystemTime>='{1}' and @SystemTime<='{2}']]]",
                    providerName,
                    afterTimestamp.ToUniversalTime().ToString("o"),
                    beforeTimestamp.ToUniversalTime().ToString("o"));
                AddEventRecord(logName, queryString);
            }

            return outputTable;
        }

        private void AddEventRecord(string logName, string queryString)
        {

            var beforeCulture = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

                EventLogQuery query = new EventLogQuery(logName, PathType.LogName, queryString);

                using (EventLogReader reader = new EventLogReader(query))
                {
                    for (var eventRecord = reader.ReadEvent(); eventRecord != null; eventRecord = reader.ReadEvent())
                    {
                        // Read event records
                        string message = eventRecord.FormatDescription();
                        outputTable.Rows.Add(eventRecord.RecordId, eventRecord.LevelDisplayName, eventRecord.TimeCreated, eventRecord.ProviderName,
                                             message);
                    }
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = beforeCulture;
            }
        }
        public DataTable GetEventRecords(DateTime afterTimestamp, DateTime beforeTimestamp, string level)
        {
            //Critical	Value: 1. Indicates logs for a critical alert.
            //Error	Value: 2. Indicates logs for an error.
            //Information	Value: 4. Indicates logs for an informational message.
            //Undefined	Value: 0. Indicates logs at all levels.
            //Verbose	Value: 5. Indicates logs at all levels.
            //Warning	Value: 3. Indicates logs for a warning.
            if (!String.IsNullOrEmpty(level))
            {
                switch (level)
                {
                    case "Error":
                        level = "2";
                        break;
                    case "error":
                        level = "2";
                        break;
                    case "Critical":
                        level = "1";
                        break;
                    case "critical":
                        level = "2";
                        break;
                    case "Information":
                        level = "4";
                        break;
                    case "information":
                        level = "4";
                        break;
                    case "Info":
                        level = "4";
                        break;
                    case "info":
                        level = "4";
                        break;
                    case "Warning":
                        level = "3";
                        break;
                    case "warning":
                        level = "3";
                        break;
                    case "w":
                        level = "3";
                        break;
                    case "c":
                        level = "1";
                        break;
                    case "e":
                        level = "2";
                        break;
                    case "i":
                        level = "4";
                        break;
                    case "W":
                        level = "3";
                        break;
                    case "C":
                        level = "1";
                        break;
                    case "E":
                        level = "2";
                        break;
                    case "I":
                        level = "4";
                        break;

                }
            }
            outputTable = GetEventRecords(afterTimestamp, beforeTimestamp, level, "System", null);
            outputTable.Merge(GetEventRecords(afterTimestamp, beforeTimestamp, level, "Application", null), false, MissingSchemaAction.Add);
            return outputTable;
        }
    }
}
