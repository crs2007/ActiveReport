using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;
using System.Data.SqlClient;
using System.Management;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.ServiceModel;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.IO.Compression;
using System.Net.Http;
using System.Net;

namespace ActiveReport.Class
{

    public static class ConfigurationHandler
    {
        #region JSON Config Helper
        private static string json;
        private static string _SQLPrefix = @"SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET ARITHABORT ON;
SET LOCK_TIMEOUT 2000;
SET DEADLOCK_PRIORITY LOW;
";
        public static string GetAppTitle()
        {
            AssemblyTitleAttribute attributes = (AssemblyTitleAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyTitleAttribute), false);

            return attributes.Title;
        }

		public static XNamespace GetXsiNs()
		{

			XNamespace xsiNs = "http://www.w3.org/2001/XMLSchema-instance";
			return xsiNs;
		}

		public static string GetSQLPrefix()
        {
            return _SQLPrefix;
        }

		public static int GetSQLQueryTimeout()
		{
			int _MaxSQLQueryTimeout = 30;
			try
			{
				_MaxSQLQueryTimeout = Int32.Parse(GetConfigValue("MaxSQLQueryTimeout"));
			}
			catch (Exception)
			{

				_MaxSQLQueryTimeout = 30;
			}
			return _MaxSQLQueryTimeout;
		}

        public static string GetClientName()
        {
            string _ClientName = "General Client";
            try
            {
                _ClientName = GetConfigValue("ClientName");
            }
            catch (Exception)
            {

                _ClientName = "General Client";
            }
            return _ClientName;
        }

		public static void createDir(string FullPath)
        {
            FileInfo fileInfo = new FileInfo(FullPath);

            if (!fileInfo.Exists)
                Directory.CreateDirectory(fileInfo.Directory.FullName);
        }

        public static string GetConfigValue(string Key)
        {
            string strReturn = "No key configuration has been entered!";
            if (!(string.IsNullOrEmpty(Key)))
            {
                load(@"config.json");
                try
                {
                    JArray ConfigItemArray = JArray.Parse(json);
                    strReturn = ConfigItemArray[0][Key].ToString();
                }
                catch (Exception)
                {

                    throw;
                }
                
            }
            return strReturn;
        }

        public static List<SQLScript> getSQLFileContent()
        {
            string[] fileArray = getSQLFiles("sql");
            List<SQLScript> SQLFiles = new List<SQLScript>();
            string ClientID = GetConfigValue("ClientID");
            string ClientName = GetConfigValue("ClientName");
            string SQLReport_Metadata = "SELECT NEWID() AS id, '@ClientID' AS[ClientID], '@ClientName' AS[Client], GETDATE() AS[date], SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS[ServerName], '3.0'[ClientVersion]";
            SQLReport_Metadata = SQLReport_Metadata.Replace("@ClientID", ClientID);
            SQLReport_Metadata = SQLReport_Metadata.Replace("@ClientName", ClientName);
            SQLFiles.Add(new SQLScript("Report_Metadata", SQLReport_Metadata));
            
            foreach (var file in fileArray)
            {

                try
                {   // Open the text file using a stream reader.
                    using (StreamReader sr = new StreamReader(file))
                    {
                        // Read the stream to a string, and write the string to the console.
                        String Content = sr.ReadToEnd();
                        SQLFiles.Add(new SQLScript(Path.GetFileName(file).Replace(".sql",""), Content));
                    }
                }
                catch (Exception e)
                {

                    throw e.InnerException;
                }
            }
            return SQLFiles;
        }

        private static string[] getSQLFiles(string type)
        {
            type = "*." + type;
            string[] fileArray;
            string SQLFilesPath = Directory.GetCurrentDirectory() + @"\SQLFiles\";
            try
            {
                fileArray = Directory.GetFiles(SQLFilesPath, type);
            }
            catch (Exception)
            {

                throw;
            }
            
            return fileArray;
        }

        private static void load(string configFile)
        {
            String FilePath;
            FilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
            StreamReader r = new StreamReader(FilePath);
            json = r.ReadToEnd();

        }

        public static List<string> getConectionString()
        {
            List<string> ConectionString = new List<string>();
            string defaultConnectionString;
            try
            {


            load(@"ServerList.json");

            var ConfigItemArray = JObject.Parse(json);
            defaultConnectionString = (string)ConfigItemArray["defaultConnectionString"];
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder();

            builder.ConnectionString = defaultConnectionString;
            SqlConnectionStringBuilder mySQLBuilder = new SqlConnectionStringBuilder();
            if (mySQLBuilder.ContainsKey("Initial Catalog"))
                mySQLBuilder.InitialCatalog = builder["Initial Catalog"] as string;
            else mySQLBuilder.InitialCatalog = "master";
            if (mySQLBuilder.ContainsKey("Application Name"))
                mySQLBuilder.ApplicationName = builder["Application Name"] as string;
            else mySQLBuilder.ApplicationName = "NAYATechActiveReport";

            var ServersArray = ConfigItemArray.Children<JProperty>().FirstOrDefault(x => x.Name == "Servers").Value;
            foreach (var item in ServersArray.Children())
            {
                var itemProperties = item.Children<JProperty>();
                //you could do a foreach or a linq here depending on what you need to do exactly with the value
                var myElement = itemProperties.FirstOrDefault(x => x.Name == "connectionString");
                var myElementValue = myElement.Value; ////This is a JValue type
                string IntegratedSecurity = "False";
                if (builder.ContainsKey("Integrated Security"))
                    IntegratedSecurity =builder["Integrated Security"] as string;
                builder.ConnectionString = myElementValue.ToString();
                mySQLBuilder.DataSource = builder["Data Source"] as string;
                if (builder.ContainsKey("User Id") && builder.ContainsKey("Password"))
                {
                    mySQLBuilder.UserID = builder["User Id"] as string;
                    mySQLBuilder.Password = builder["Password"] as string;
                    mySQLBuilder.IntegratedSecurity = false;
                }
                else if (IntegratedSecurity == "SSPI" || IntegratedSecurity == "True")
                    mySQLBuilder.IntegratedSecurity = true;
                ConectionString.Add(mySQLBuilder.ConnectionString);
            }
            }
            catch (Exception ex)
            {

                throw new Exception("There is a problem with 'ServerList.json' file. Please make sure all Connections String are formatted correctly", ex);
            }
            
            return ConectionString;
        }
		#endregion

        #region File Handle Helper
        public static void DeleteFile(string FilePath)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        /// <summary>
        /// Create a ZIP file of the files provided.
        /// </summary>
        /// <param name="fileName">The full path and name to store the ZIP file at.</param>
        /// <param name="files">The list of files to be added.</param>
        public static void CreateZipFile(string fileName, IEnumerable<string> files)
        {
            // Create and open a new ZIP file
            DeleteFile(fileName);
            var zip = ZipFile.Open(fileName, ZipArchiveMode.Create);
            foreach (var file in files)
            {
                // Add the entry for each file
                zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
            }
            // Dispose of the object when we are done
            zip.Dispose();
        }


        public static string getUpgradeInfo()
        {
            var http = new HttpClient();
            string versionString = "";// = await http.GetStringAsync(new Uri("https://github.com/crs2007/ActiveReport/tree/master/ActiveReport/latest.txt"));
            Version latestVersion = new Version(versionString);

            //get my own version to compare against latest.
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            Version myVersion = new Version(fvi.ProductVersion);

            if (latestVersion > myVersion)
            {
                return String.Format("You've got version {0} of SecretStartup for Windows. Would you like to update to the latest version {1}?", myVersion, latestVersion);
            }
            return String.Empty;
        }

        public static void downloadFileFromGitHub()
        {
            string destinationPath = AppDomain.CurrentDomain.BaseDirectory + @"SQLFiles\";
            foreach(var file in Directory.GetFiles(destinationPath, "*.sql"))
            {
                downloadFileFromGitHub(file.ToString());
            }

        }

        private static void downloadFileFromGitHub(string FileName)
        {
            string sourceURL = @"https://github.com/crs2007/ActiveReport/tree/master/ActiveReport/SQLFiles";
            string destinationPath = FileName;
            long fileSize = 0;
            int bufferSize = 1024;
            bufferSize *= 1000;
            long existLen = 0;

            FileStream saveFileStream;
            if (File.Exists(destinationPath))
            {
                FileInfo destinationFileInfo = new FileInfo(destinationPath);
                existLen = destinationFileInfo.Length;
            }

            

            if (existLen > 0)
                using (var stream = File.Open(destinationPath, FileMode.Append,
                                                          FileAccess.Write,
                                                          FileShare.ReadWrite))
                {
                    saveFileStream = stream;
                }
            else
                using (var stream = File.Open(destinationPath, FileMode.Open,
                                                          FileAccess.Write,
                                                          FileShare.ReadWrite))
                {
                    saveFileStream = stream;
                }

            HttpWebRequest httpReq;
            HttpWebResponse httpRes;
            httpReq = (HttpWebRequest)HttpWebRequest.Create(sourceURL);
            httpReq.AddRange((int)existLen);
            System.IO.Stream resStream;
            httpRes = (HttpWebResponse)httpReq.GetResponse();
            resStream = httpRes.GetResponseStream();

            fileSize = httpRes.ContentLength;

            int byteSize;
            byte[] downBuffer = new byte[bufferSize];

            while ((byteSize = resStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
            {
                saveFileStream.Write(downBuffer, 0, byteSize);
            }
        } 

        #endregion

        #region SQLServer Helper
        public static bool PingHost(string nameOrAddress)
		{
			bool pingable = false;
			Ping pinger = new Ping();
			nameOrAddress = (nameOrAddress == ".") ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : nameOrAddress;
			try
			{
				PingReply reply = pinger.Send(nameOrAddress);
				pingable = reply.Status == IPStatus.Success;
			}
			catch (PingException)
			{
				return false;
			}
			return pingable;
		}

		public static string checkSQLServerConnection(string connectionString, string SQLPrefix)
        {
            string serverName;
			string fullServerName;
			fullServerName = getServerNameFromConnectionString(connectionString);
			serverName = fullServerName.Split(new Char[] { '\\' })[0];
			if (PingHost(serverName))
			{
				using (var conn = new SqlConnection(connectionString))
				{

					try
					{
						conn.Open();
						var cmd = conn.CreateCommand();
						cmd.CommandText = SQLPrefix + "SELECT @@SERVERNAME;";
						serverName = cmd.ExecuteScalar().ToString();
						cmd.Dispose();
					}
					catch (Exception serverNameError)
					{
						serverName = fullServerName;
						throw new Exception(String.Concat("\nError when connecting on server:: ", serverName,
							"\nconnection string: ", connectionString,
							"\nError: ", serverNameError.Message.Replace(".", ".\n"),
							"\n", Environment.NewLine), serverNameError);
					}
				}
			}
			else
			{
				throw new Exception(String.Concat("\nError when connecting on server:: ", fullServerName,
							"\nconnection string: ", connectionString,
							"\nError: Server is unreachable to ping test.", Environment.NewLine));
				//return fullServerName;
			}
            return serverName;
        }

		public static string getServerNameFromConnectionString(string connectionString)
		{
			DbConnectionStringBuilder connBuilder = new DbConnectionStringBuilder();
			connBuilder.ConnectionString = connectionString;
			return connBuilder["Data Source"] as string;
		}

        public static string SafeGetString(this SqlDataReader reader, int colIndex)
        {
            if (!reader.IsDBNull(colIndex))
                return reader.GetString(colIndex);
            return string.Empty;
        }

        #region Windows Event viewer
        //TODO
        #endregion

        #region SQLServer Error
        private static string getRegexUserName(string line)
        {
            if (Regex.IsMatch(line, @"^Login failed for user '([\W\w]+)'\. Reason: Failed to open the explicitly specified database '([\W\w]+)'\. \[CLIENT: ([\W\w]+)\]"))
                return Regex.Replace(line, @"^Login failed for user '([\W\w]+)'\. Reason: Failed to open the explicitly specified database '([\W\w]+)'\. \[CLIENT: ([\W\w]+)\]", "$1");
            if (Regex.IsMatch(line, @"^ Login failed for user '([\W\w] +) '\. Reason: Failed to open the database '([\W\w] +)' specified in the login properties\. \[CLIENT: ([\W\w]+)\]"))
                return Regex.Replace(line, @"^ Login failed for user '([\W\w]+)'\. Reason: Failed to open the database '([\W\w]+)' specified in the login properties\. \[CLIENT: ([\W\w]+)\]", "$1");
            if (Regex.IsMatch(line, @"^Login failed for user '([\W\w]+)'\. Reason: Password did not match that for the login provided\. \[CLIENT: ([\W\w]+)\]"))
                return Regex.Replace(line, @"^Login failed for user '([\W\w]+)'\. Reason: Password did not match that for the login provided\. \[CLIENT: ([\W\w]+)\]", "$1");
            if (Regex.IsMatch(line,@"^The service account is '([\W\w]+)'\. This is an informational message\; no user action is required\." ))
                return Regex.Replace(line, @"^The service account is '([\W\w]+)'\. This is an informational message\; no user action is required\.", "$1");
            if (Regex.IsMatch(line, @"^Login failed for user '([\W\w]+)'\. Reason: Could not find a login matching the name provided\. \[CLIENT: ([\W\w]+)\]"))
                return Regex.Replace(line, @"^Login failed for user '([\W\w]+)'\. Reason: Could not find a login matching the name provided\. \[CLIENT: ([\W\w]+)\]", "$1");
            return null;
        }

        private static string getRegexDatabaseName(string line)
        {


            if (Regex.IsMatch(line, @"^Login failed for user '([\W\w]+)'\. Reason: Failed to open the explicitly specified database '([\W\w]+)'\. \[CLIENT: ([\W\w]+)\]"))
                return Regex.Replace(line, @"^Login failed for user '([\W\w]+)'\. Reason: Failed to open the explicitly specified database '([\W\w]+)'\. \[CLIENT: ([\W\w]+)\]", "$2");
            if (Regex.IsMatch(line, @"^Login failed for user '([\W\w]+)'\. Reason: Failed to open the database '([\W\w]+)' specified in the login properties\. \[CLIENT: ([\W\w]+)\]"))
                return Regex.Replace(line, @"^Login failed for user '([\W\w]+)'\. Reason: Failed to open the database '([\W\w]+)' specified in the login properties\. \[CLIENT: ([\W\w]+)\]", "$2");
            if (Regex.IsMatch(line, @"^Restore is complete on database '([\W\w]+)'.  The database is now available."))
                return Regex.Replace(line, @"^Restore is complete on database '([\W\w]+)'.  The database is now available.", "$1");
            if (Regex.IsMatch(line, @"^\[INFO\] Database ID: \[([\d]+)\]. Hardened root content for checkpoint timestamp"))
                return Regex.Replace(line, @"^\[INFO\] Database ID: \[([\d]+)\]. Hardened root content for checkpoint timestamp [\W\w]*", "$1");
            if (Regex.IsMatch(line, @"^\[INFO\] HkHostLogCheckpointRecord\(\): Database ID: \[([\d]+)\]. Logged XTP checkpoint"))
                return Regex.Replace(line, @"^\[INFO\] HkHostLogCheckpointRecord\(\): Database ID: \[([\d]+)\]. Logged XTP checkpoint [\W\w]*", "$1");
            if (Regex.IsMatch(line, @"^\[INFO\] HkCheckpointCtxtImpl::StartOfflineCkpt\(\): Database ID: \[([\d]+)\]"))
                return Regex.Replace(line, @"^\[INFO\] HkCheckpointCtxtImpl::StartOfflineCkpt\(\): Database ID: \[([\d]+)\][\W\w]*", "$1");
            if (Regex.IsMatch(line, @"^AppDomain [\d]+ \(([\W\w]+)\.dbo\[runtime\]\.[\d]+\)"))
                return Regex.Replace(line, @"^AppDomain [\d]+ \(([\W\w]+)\.dbo\[runtime\]\.[\d]+\)[\W\w]*", "$1");
            if (Regex.IsMatch(line, @"^SQL Server has encountered [\d]+ occurrence\(s\) of I\/O requests taking longer than [\d]+ seconds to complete on file[\W\w]*"))
                return Regex.Replace(line, @"^SQL Server has encountered [\d]+ occurrence\(s\) of I\/O requests taking longer than [\d]+ seconds to complete on file \[[\W\w]*\] in database id ([\d]+)[\W\w]*", "$1");
            if (Regex.IsMatch(line, @"^The database '([\W\w]+)' is marked RESTORING and is in a state that does not allow recovery to be run\."))
                return Regex.Replace(line, @"^The database '([\W\w]+)' is marked RESTORING and is in a state that does not allow recovery to be run\.", "$1");
            if (Regex.IsMatch(line, @"^Database '([\W\w]+)' is in transition\. Try the statement later\."))
                return Regex.Replace(line, @"^Database '([\W\w]+)' is in transition\. Try the statement later\.", "$1");
            return null;
        }

        private static string getRegexMessage(string line)
        {
            if (Regex.IsMatch(line, @"^ AppDomain[\d] + \(([\W\w] +)\.dbo\[runtime\]\.[\d]+\)"))
                return Regex.Replace(line, @"^AppDomain [\d]+ \(([\W\w]+)\.dbo\[runtime\]\.[\d]+\)[\W\w]*", @"CLR User AppDomain");
            if (Regex.IsMatch(line, @"^\[INFO\] Database ID: \[([\d]+)\]. Hardened root content for checkpoint timestamp"))
                return Regex.Replace(line, @"^\[INFO\] Database ID: \[([\d]+)\]. Hardened root content for checkpoint timestamp [\W\w]*", "Info: Logged XTP checkpoint");
            if (Regex.IsMatch(line, @"^\[INFO\] HkHostLogCheckpointRecord\(\): Database ID: \[([\d]+)\]. Logged XTP checkpoint"))
                return Regex.Replace(line, @"^\[INFO\] HkHostLogCheckpointRecord\(\): Database ID: \[([\d]+)\]. Logged XTP checkpoint [\W\w]*",@"Info: Logged XTP checkpoint");
            if (Regex.IsMatch(line, @"^\[INFO\] HkCheckpointCtxtImpl::StartOfflineCkpt\(\): Database ID: \[([\d]+)\]"))
                return Regex.Replace(line, @"^\[INFO\] HkCheckpointCtxtImpl::StartOfflineCkpt\(\): Database ID: \[([\d]+)\][\W\w]*","Info: Logged XTP checkpoint");
            if (Regex.IsMatch(line, @"Restore is complete on database"))
                return @"Restore is complete on database";
            if (Regex.IsMatch(line, @"This is an informational message only\. No user action is required") || Regex.IsMatch(line, @"^Setting database option") || Regex.IsMatch(line, @"^Starting up database") || Regex.IsMatch(line, @"^Synchronize Database") || Regex.IsMatch(line, @"This is an informational message only\; no user action is required"))
                return @"This is an informational message only. No user action is required";
            if (Regex.IsMatch(line, @"^SQL Server has encountered[\d]+ occurrence\(s\) of I\/O requests taking longer than[\d]+ seconds to complete on file"))
                return @"SQL Server has encountered occurrence(s) of I/O requests taking longer then it supposed";
            if (Regex.IsMatch(line, @"^The database '([\W\w]+)' is marked RESTORING and is in a state that does not allow recovery to be run\."))
                return @"The database is marked RESTORING and is in a state that does not allow recovery to be run.";
                                  
            return null;
        }

        private static List<SQLErrorDetailLog> getSQLServerErrorDetail(List<SQLErrorLog> errorLog)
        {
            return (List<SQLErrorDetailLog>)(from Log in errorLog
                                             where Regex.IsMatch(Log.Message, @"^Error:\s+([0-99999]+),\s+Severity:\s+([\d]+),\s+State:\s+([\d]+)\.")
                                           select new SQLErrorDetailLog()
                                           {
                                               LogDate = Log.LogDate,
                                               ProcessesInfo = Log.ProcessesInfo,
                                               Error = Regex.Replace(Log.Message, @"^Error:\s+([0-99999]+),\s+Severity:\s+([\d]+),\s+State:\s+([\d]+)\.","$1"),
                                               Severity = Regex.Replace(Log.Message, @"^Error:\s+([0-99999]+),\s+Severity:\s+([\d]+),\s+State:\s+([\d]+)\.", "$2"),
                                               State = Regex.Replace(Log.Message, @"^Error:\s+([0-99999]+),\s+Severity:\s+([\d]+),\s+State:\s+([\d]+)\.", "$3")
                                           }).ToList();
        }

        public static XElement getSQLServerError(string connectionString)
        {

            XNamespace xsiNs = GetXsiNs();
            List<SQLErrorLog> SQLErrorLogHistory = new List<SQLErrorLog>();

            string SQLStr = String.Concat(_SQLPrefix, @"
DECLARE @errorlog INT;
DECLARE @errorcount INT;
DECLARE @StartDate DATETIME;
DECLARE @EndDate DATETIME;
SELECT	@StartDate = DATEADD(WW, -1, GETDATE()),
		@EndDate = GETDATE(); 
SET @errorlog = 0;
SET @errorcount = 0;
IF OBJECT_ID('tempdb..#errorlog_stage') IS NOT NULL
        DROP TABLE #errorlog_stage;
DECLARE @NumErrorLogs INT;
IF (NOT IS_SRVROLEMEMBER(N'securityadmin') = 1) 
BEGIN 
    RAISERROR(15003,-1,-1, N'securityadmin');
END
SET @NumErrorLogs = 6;
CREATE TABLE #errorlog_stage
    (
        LogDate DATETIME ,
        ProcessesInfo VARCHAR(100) ,
        Text VARCHAR(MAX)
    );
 DECLARE @FileList AS TABLE (
  subdirectory NVARCHAR(4000) NOT NULL 
  ,DEPTH BIGINT NOT NULL
  ,[FILE] BIGINT NOT NULL
 );

DECLARE @ErrorLogFileName NVARCHAR(4000), @ErrorLogPath NVARCHAR(4000);
SELECT @ErrorLogFileName = CAST(SERVERPROPERTY(N'errorlogfilename') AS NVARCHAR(4000));
DECLARE	@IsLinux BIT;
SET @IsLinux = 0;
IF OBJECT_ID('sys.dm_os_host_info') IS NOT NULL
BEGIN
	SELECT	@IsLinux = 1
	FROM	sys.dm_os_host_info  
END
IF @IsLinux = 0
BEGIN
 SELECT @ErrorLogPath = SUBSTRING(@ErrorLogFileName, 1, LEN(@ErrorLogFileName) - CHARINDEX(N'\', REVERSE(@ErrorLogFileName))) + N'\';
END
ELSE
BEGIN
 SELECT @ErrorLogPath = SUBSTRING(@ErrorLogFileName, 1, LEN(@ErrorLogFileName) - CHARINDEX(N'/', REVERSE(@ErrorLogFileName))) + N'/';    
END
 INSERT INTO @FileList
 EXEC xp_dirtree @ErrorLogPath, 0, 1;
 
SET @NumErrorLogs = (SELECT COUNT(*) FROM @FileList WHERE [@FileList].subdirectory LIKE N'ERRORLOG%');

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,NULL, NULL, @StartDate, @EndDate;
SET @errorlog = @errorlog + 1;


WHILE ( @errorcount < 1 AND @errorlog < @NumErrorLogs)
BEGIN
    BEGIN TRY
        INSERT  #errorlog_stage
                EXEC master.dbo.xp_readerrorlog @errorlog, 1,NULL, NULL, @StartDate, @EndDate;
        SET @errorlog = @errorlog + 1;

                     
    END TRY
    BEGIN CATCH
        SET @errorcount = @errorcount + 1;
		SELECT @errorcount
    END CATCH;
END;

SELECT  ES.LogDate , ES.ProcessesInfo, ES.Text Message
FROM    #errorlog_stage ES
WHERE   ES.LogDate > DATEADD(WW, -1, GETDATE())
	    AND ES.Text NOT IN ('(c) Microsoft Corporation.','All rights reserved.','Authentication mode is MIXED.',
            'SQL Trace ID 2 was started by login ""sa"".','SQL Trace stopped. Trace ID = ''2''. Login Name = ''sa''.','The error log has been reinitialized. See the previous log for older entries.',
            'Default collation: Hebrew_CI_AS (us_english 1033)', 'System Manufacturer: ''VMware, Inc.'', System Model: ''VMware Virtual Platform''.', 'Resource governor reconfiguration succeeded.',
            'Clearing tempdb database.', 'System Manufacturer: ''VMware, Inc.'', System Model: ''VMware Virtual Platform''.', 'Resource governor reconfiguration succeeded.',
                    'Using conventional memory in the memory manager.', 'The Service Broker endpoint is in disabled or stopped state.',
                    'The maximum number of dedicated administrator connections for this instance is ''1''',
                'The Database Mirroring endpoint is in disabled or stopped state.',
                'SQL Trace ID 1 was started by login ""sa"".', 'Software Usage Metrics is disabled.',
                'Service Broker manager has started.',
                'A new instance of the full-text filter daemon host process has been successfully started.',
                'A self-generated certificate was successfully loaded for encryption., Server is listening on [ ''any'' <ipv6> 1433]., Server is listening on [ ''any'' <ipv4> 1433].',
                'Server local connection provider is ready to accept connection on [ \\.\pipe\SQLLocal\MSSQLSERVER ]., Server local connection provider is ready to accept connection on [ \\.\pipe\sql\query ].'

                    )

            AND ES.ProcessesInfo != 'Login'
            AND ES.Text NOT LIKE '%without error%'
            AND ES.Text NOT LIKE 'Starting up database%'
            AND ES.Text NOT LIKE 'BACKUP DATABASE successfully%'
            AND ES.Text NOT LIKE  '%transactions rolled back in database%'
            AND ES.Text NOT LIKE 'Recovery completed for database %'
            AND ES.Text NOT LIKE '%No user action%'
            AND ES.Text NOT LIKE '%AppDomain%'
            AND ES.Text NOT LIKE '%found 0 errors%'
            AND ES.Text NOT LIKE '%RESTORE DATABASE successfully processed%'
            AND ES.Text NOT LIKE '%Setting database option%'


            AND ES.Text NOT LIKE '%index restored%'


            AND ES.Text NOT LIKE '%Setting database option%'

            AND ES.Text NOT LIKE 'Server is listening on%'

            AND ES.Text NOT LIKE 'A self-generated certificate was successfully loaded for encryption%'

            AND ES.Text NOT LIKE '(c) Microsoft Corporation%'

            AND ES.Text NOT LIKE 'Server local connection provider is ready to accept connection on%'

            AND ES.Text NOT LIKE 'Dedicated admin connection support was established for listening locally on port%'

            AND ES.Text NOT LIKE 'System Manufacturer%'



            AND ES.Text NOT LIKE '%was killed by hostname%'
            AND ES.Text NOT LIKE '%Authentication mode is%'


            AND ES.Text NOT LIKE 'CLR version%'
            AND ES.Text NOT LIKE 'Common language runtime (CLR) functionality initialized using CLR version%'
            AND ES.Text NOT LIKE 'UTC adjustment%'


            AND ES.Text NOT LIKE 'Default collation%'


            AND ES.Text NOT LIKE 'Error: 701%'
            AND ES.Text NOT LIKE '%There is insufficient system memory in resource pool%'

            AND ES.Text NOT LIKE 'Error: 17300%'

            AND ES.Text NOT LIKE 'Error: 17312%'

            AND ES.Text NOT LIKE '%The error is printed in terse mode because there was error during formatting. Tracing, ETW, notifications etc are skipped.%'

            AND ES.Text NOT LIKE 'A significant part of sql server process memory has been paged out. This may result in a performance degradation.%'

            AND ES.Text NOT LIKE '%Failed allocate pages%'

            AND ES.Text NOT LIKE '%OBJECTSTORE_LOCK_MANAGER%'

            AND ES.Text NOT LIKE '%MEMORYCLERK_SQLBUFFERPOOL%'

            AND ES.Text NOT LIKE '%Procedure Cache%'

            AND ES.Text NOT LIKE '%OBJECTSTORE_SECAUDIT_EVENT_BUFFER%'

            AND ES.Text NOT LIKE '%FAIL_PAGE_ALLOCATION%'


            AND ES.Text NOT LIKE 'FILESTREAM: effective level%'

            AND ES.Text NOT LIKE 'Microsoft SQL Server 20%'
            AND ES.Text NOT LIKE 'Query Store settings initialized with enabled%'
            AND ES.Text NOT LIKE 'Restore is complete on database%'
            AND ES.Text NOT LIKE '%is marked RESTORING and is in a state that does not allow recovery to be run.'
            AND ES.Text NOT LIKE '%with Resource Database.'                                  


            AND ES.Text NOT LIKE 'DBCC TRACEOFF 3604%'
            AND ES.Text NOT LIKE 'DBCC TRACEON 3604%'
            AND ES.Text NOT LIKE 'The activated proc%'
			
            AND ES.Text NOT LIKE 'Configuration option%'
			
            AND ES.Text NOT LIKE 'SQL Trace%'
        OPTION(RECOMPILE);

        DROP TABLE #errorlog_stage;
");

            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = SQLStr;
					cmd.CommandTimeout = GetSQLQueryTimeout();

					using (var reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                            SQLErrorLogHistory.Add(new SQLErrorLog { LogDate = reader.GetDateTime(0), Message = reader.GetString(1)});
                    }

                    cmd.Dispose();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }


            List<SQLErrorDetailLog> detailLog = getSQLServerErrorDetail(SQLErrorLogHistory);
            

            List<SQLErrorFullDetailLog> xOutput = (List<SQLErrorFullDetailLog>)(from LogHistory in SQLErrorLogHistory
                                                 join dLog in detailLog on
                                                    new
                                                    {
                                                        JoinProperty1 = LogHistory.LogDate,
                                                        JoinProperty2 = LogHistory.ProcessesInfo
                                                    }
                                                    equals
                                                    new
                                                    {
                                                        JoinProperty1 = dLog.LogDate,
                                                        JoinProperty2 = dLog.ProcessesInfo
                                                    }
                                                 where !Regex.IsMatch(LogHistory.Message, @"^Error:\s+([0-99999]+),\s+Severity:\s+([\d]+),\s+State:\s+([\d]+)\.")

                                                                          group new { LogHistory, dLog } by new {
                                                                              Message = getRegexMessage(LogHistory.Message),
                                                                              UserName = getRegexUserName(LogHistory.Message),
                                                                              dLog.Error, dLog.Severity,
                                                                              dLog.State, LogHistory.ProcessesInfo } into grp
                                                                          select new SQLErrorFullDetailLog()
                                                 {
                                                     Message = grp.Key.Message,
                                                     UserName = grp.Key.UserName,
                                                     Error = grp.Key.Error,
                                                     Severity = grp.Key.Severity,
                                                     State = grp.Key.State,
                                                     ProcessesInfo = grp.Key.ProcessesInfo,
                                                     Counts = grp.Count(),
                                                     Databases = string.Join(",", grp.Select(db => getRegexDatabaseName(db.LogHistory.Message))),
                                                     OriginalMessage = grp.Max(om => om.LogHistory.Message)
                                                 }).ToList();





            return new XElement("ErrorLog",
            from x in xOutput
            select new XElement(xsiNs + "ErrorLog_Data",
                         new XElement("Message", x.Message),
                         new XElement("UserName", x.UserName),
                         new XElement("Error", x.Error),
                         new XElement("Severity", x.Severity),
                         new XElement("State", x.State),
                         new XElement("ProcessesInfo", x.ProcessesInfo),
                         new XElement("Count", x.Counts),
                         new XElement("OriginalMessage", x.OriginalMessage)
                       ));
        }

        #endregion

        #region SQLServer Backup
        public static XElement getSQLServerBackup(string connectionString)
        {

            XNamespace xsiNs = GetXsiNs();
            List<SQLBackup> SQLBackupHistory = new List<SQLBackup>();
            List<SQLBackupSingle> SQLBackupOutput = new List<SQLBackupSingle>();
            List<SQLNodeOut> SQLNode = new List<SQLNodeOut>();

            SQLNode = getClusterNodes(connectionString);
            if (SQLNode.Count == 0)
            {
                SQLNode.Add(new SQLNodeOut { NodeName = ConfigurationHandler.getServerNameFromConnectionString(connectionString) });
            }
            string SQLStr = String.Concat(_SQLPrefix, @"
SELECT  @@SERVERNAME [ServerName],t1.DatabaseName ,
        CONVERT(VARCHAR(50), t1.LastBackUpTime, 101) [LastBackUpTime],
        t1.Type ,
        t2.physical_device_name
FROM    ( SELECT    sdb.name AS DatabaseName ,
                    MAX(ISNULL(bus.backup_finish_date, 0)) AS LastBackUpTime ,
                    bus.type AS Type
          FROM      sys.databases sdb
                    LEFT JOIN msdb..backupset bus ON bus.database_name = sdb.name
                    LEFT JOIN sys.database_mirroring dm ON sdb.database_id = dm.database_id
          WHERE     sdb.database_id <> 2
                    AND sdb.source_database_id IS NULL
                    AND ( dm.mirroring_role = 1
                          OR mirroring_guid IS NULL
                        )
                    AND sdb.state <> 6
                    AND ( ( sdb.recovery_model = 3
                            AND ISNULL(bus.type, 'D') = 'D'
                          )
                          OR ( sdb.recovery_model <> 3 )
                        )
          GROUP BY  sdb.name ,
                    bus.type
        ) t1
        LEFT JOIN ( SELECT  sd.name AS database_name ,
                            ISNULL(bu.backup_finish_date, 0) AS BackupDate ,
                            bmf.physical_device_name
                    FROM    sys.databases AS sd
                            LEFT JOIN msdb..backupset bu ON bu.database_name = sd.name
                                                            AND bu.is_copy_only = 0
                            LEFT JOIN msdb..backupmediafamily bmf ON bu.media_set_id = bmf.media_set_id
                  ) t2 ON t1.DatabaseName = t2.database_name
                          AND t1.LastBackUpTime = t2.BackupDate
OPTION(RECOMPILE);");
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
            foreach (var node in SQLNode)
            {


                builder.ConnectionString = connectionString;
                builder["Data Source"] = node.NodeName;

                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    try
                    {
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = SQLStr;
						cmd.CommandTimeout = GetSQLQueryTimeout();

						using (var reader = cmd.ExecuteReader())
                        {

                            while (reader.Read())
                                SQLBackupHistory.Add(new SQLBackup { ServerName = SafeGetString(reader, 0), DatabaseName = SafeGetString(reader, 1), LastBackUpTime = SafeGetString(reader, 2), Type = SafeGetString(reader, 3), physical_device_name = SafeGetString(reader, 4) });
                        }

                        cmd.Dispose();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }

            
            SQLBackupOutput = (List<SQLBackupSingle>)(from Backup in SQLBackupHistory
                                                   where !String.IsNullOrEmpty(Backup.Type)
                                                   group Backup by new { Backup.DatabaseName, Backup.Type } into grp
                                                select new SQLBackupSingle()
                                                   {
                                                       DatabaseName = grp.Key.DatabaseName,
                                                        Type = grp.Key.Type,
                                                        LastBackUpTime = grp.Max(Backup => Backup.LastBackUpTime),
                                                       physical_device_name = grp.Max(Backup => Backup.physical_device_name)
                                                   }).ToList();





            return new XElement(xsiNs + "Urgent_Backup",
            from xSQLBackup in SQLBackupOutput
            select new XElement(xsiNs + "Urgent_Backup_Data",
                         new XElement(xsiNs + "DatabaseName", xSQLBackup.DatabaseName),
                         new XElement(xsiNs + "LastBackUpTime", xSQLBackup.LastBackUpTime),
                         new XElement(xsiNs + "Type", xSQLBackup.Type),
                         new XElement(xsiNs + "physical_device_name", xSQLBackup.physical_device_name)
                       ));
        }

        #endregion

        #region SQLServer Logins
        public static XElement getUnevenLogins(string connectionString, string currentServer)
        {

            XNamespace xsiNs = GetXsiNs(); 
            List<InvalidLogin> InvalidLoginOut = new List<InvalidLogin>();
            List<SQLNodeOut> SQLNode = new List<SQLNodeOut>();
            List<SQLLogin> serverLogin = new List<SQLLogin>();

            SQLNode = getClusterNodes(connectionString);

            if(SQLNode.Count <= 1)
                return new XElement("InvalidLogin",
            from InvalidLogin in InvalidLoginOut
            select new XElement(xsiNs + "InvalidLogin_Data",
                         new XElement("Text", InvalidLogin.Text)
                       ));
            string SQLStr = String.Concat(_SQLPrefix, @"
SELECT   @@SERVERNAME ServerName,
         name,
         master.dbo.fn_varbintohexstr(sid) [sid]
FROM     sys.server_principals
WHERE    type = 'S' 
         AND principal_id > 10
         AND is_disabled = 0
         AND SERVERPROPERTY('IsIntegratedSecurityOnly') = 0
OPTION(RECOMPILE);");
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
            foreach (var node in SQLNode)
            {


                builder.ConnectionString = connectionString;
                builder["Data Source"] = node.NodeName;

                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    try
                    {
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = SQLStr;
						cmd.CommandTimeout = GetSQLQueryTimeout();
						using (var reader = cmd.ExecuteReader())
                        {

                            while (reader.Read())
                                serverLogin.Add(new SQLLogin { ServerName = reader.GetString(0), name = reader.GetString(1), sid = reader.GetString(2) });
                        }

                        cmd.Dispose();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }


            if (serverLogin.Count <= 0)
                return new XElement("InvalidLogin",
            from InvalidLogin in InvalidLoginOut
            select new XElement(xsiNs + "InvalidLogin_Data",
                         new XElement("Text", InvalidLogin.Text)
                       ));

            InvalidLoginOut = (List<InvalidLogin>)(from a in serverLogin
  join b in serverLogin on a.name equals b.name 
  where a.ServerName == currentServer &&
  b.ServerName != currentServer &&
  a.sid != b.sid
                                                  select new InvalidLogin()
                         {
                             Text = String.Concat("Login:: ", a.name ," Have a different sid form Server - ", b.ServerName)
                         }).ToList();

            return new XElement("InvalidLogin",
            from InvalidLogin in InvalidLoginOut
            select new XElement(xsiNs + "InvalidLogin_Data",
                         new XElement("Text", InvalidLogin.Text)
                       ));
        }
        #endregion

        #region Node info
        public static XElement getNodesInfo(string connectionString)
        {
            //Nodes Info
            XNamespace xsiNs = GetXsiNs();
            List<Node> Nodes = null;

            try
            {
                Nodes = getClusterNodeInformation(connectionString);
            }
            catch (Exception)
            {
                throw;
            }
            return new XElement("Nodes",
            from Node in Nodes
            select new XElement(xsiNs + "Nodes_Data",
                         new XElement("Server", Node.Server),
                           new XElement("Property", Node.Property),
                           new XElement("Value", Node.Value)
                       ));

        }

        private static List<SQLNodeOut> getClusterNodes(string connectionString)
        {
            List<SQLNodeOut> SQLNode = new List<SQLNodeOut>();
            string SQLNodeStr = String.Concat(_SQLPrefix,@"
IF OBJECT_ID('tempdb..#Nodes') IS NOT NULL
    DROP TABLE #Nodes;
CREATE TABLE #Nodes
    (NodeName sysname);

IF SERVERPROPERTY('IsHadrEnabled') = 1
    EXEC ('INSERT    #Nodes            
SELECT   DISTINCT AR.replica_server_name
FROM     master.sys.availability_replicas AR
OPTION(RECOMPILE);');

SELECT NodeName
FROM   #Nodes;

DROP TABLE #Nodes;");
            
            using (var conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = SQLNodeStr;
					cmd.CommandTimeout = GetSQLQueryTimeout();
					using (var reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                            SQLNode.Add(new SQLNodeOut { NodeName = reader.GetString(0) });
                    }

                    cmd.Dispose();
                }
                catch (Exception)
                {

                }

            }
            return SQLNode;
        }

        private static List<Node> getClusterNodeInformation(string connectionString)
        {
            List<Node> result = new List<Node>();
            List<SQLNodeOut> SQLNode = getClusterNodes(connectionString);

            if (SQLNode.Count <= 0)
                return result;
            foreach (var node in SQLNode)
            {
                List<Node> midresult;
                midresult = getRemoteInfo(node.NodeName.ToString());
                result.AddRange(midresult);
            }
            return result;
        }

        private static List<Node> getRemoteInfo(string RemoteMachine)
        {
            List<Node> myRemoteMachine = null;
            ConnectionOptions ConOptions = new ConnectionOptions();
            ConOptions.Impersonation = ImpersonationLevel.Impersonate;
            //This entry required for Windows XP and newer
            ConOptions.Authentication = AuthenticationLevel.Packet;

            //Connect to WMI namespace
            ManagementScope MgtScope = new ManagementScope(@"\\" + RemoteMachine + @"\root\cimv2", ConOptions);
            MgtScope.Connect();
            if (!MgtScope.IsConnected)
            {
                return myRemoteMachine;

            }
            
            myRemoteMachine = Win32_ComputerSystem_WMI(RemoteMachine, MgtScope);
            return myRemoteMachine;



        }

        private static List<Node> Win32_ComputerSystem_WMI(string RemoteMachine, ManagementScope mgtScope)
        {
            List<Node> myRemoteMachine = new List<Node>();
            ManagementObjectSearcher ObjSearcher;
            ManagementObjectCollection Coll;// = new ManagementObjectCollection();
                                            //ManagementObject Obj;
            try
            {
                ObjSearcher = new ManagementObjectSearcher(mgtScope.Path.ToString(), "Select * FROM Win32_ComputerSystem");
                Coll = ObjSearcher.Get();
                foreach (ManagementObject Obj in Coll)
                {
                    myRemoteMachine.Add(new Node() { Server = RemoteMachine, Property = "Memory", Value = Reformat_TB_GB_MB(Obj.GetPropertyValue("TotalPhysicalMemory").ToString()) });
                }
                //Get data from  Win32_OperatingSystem WMI 
                ObjSearcher = new ManagementObjectSearcher(mgtScope.Path.ToString(), "Select * FROM Win32_OperatingSystem");
                Coll = ObjSearcher.Get();
                foreach (ManagementObject Obj in Coll)
                {
                    myRemoteMachine.Add(new Node() { Server = RemoteMachine, Property = "OS Version", Value = Obj.GetPropertyValue("Caption").ToString() });
                }

                //Get data from Win32_Processor WMI 
                ObjSearcher = new ManagementObjectSearcher(mgtScope.Path.ToString(), "Select * FROM Win32_Processor");
                Coll = ObjSearcher.Get();
                foreach (ManagementObject Obj in Coll)
                {
                    myRemoteMachine.Add(new Node() { Server = RemoteMachine, Property = "CurrentClockSpeed", Value = Reformat_GHz(Obj.GetPropertyValue("CurrentClockSpeed").ToString()) });
                    myRemoteMachine.Add(new Node() { Server = RemoteMachine, Property = "CPU Type", Value = Obj.GetPropertyValue("Name").ToString() });
                    try
                    {
                        myRemoteMachine.Add(new Node() { Server = RemoteMachine, Property = "NumberOfCores", Value = Obj.GetPropertyValue("NumberOfCores").ToString() });
                    }
                    catch (Exception)
                    {
                        
                    }
                    break;
                    
                }
                //Get data from Win32_NetworkAdapterConfiguration WMI 
                //ObjSearcher = new ManagementObjectSearcher(mgtScope.Path.ToString(), "Select * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                //Coll = ObjSearcher.Get();
                //foreach (ManagementObject Obj in Coll)
                //{
                //    myRemoteMachine.Add(new Node() { Server = RemoteMachine, Property = "IP Address", Value = Obj.GetPropertyValue("IPAddress").ToString() });
                //}
            }
            catch (Exception)
            {

                throw;
            }
            return myRemoteMachine;


        }
        #endregion
        #endregion

        #region Reformat
        private static string Reformat_GHz(string v)
        {
            string result;
            long Value = (long)Convert.ToDouble(v);
            if (Value.ToString().Length == 4) // Reformat if GHz
            {
                result = Convert.ToInt64(Value / 1000.0).ToString();
                string x = Convert.ToInt64(Value % 1000.0).ToString();
                result += '.' + x.Substring(0, (x.Length<2)?x.Length:2);
                return String.Format("{0} GHz", result);
            }


            return Value.ToString() + " Hertz";
        }

        private static string Reformat_TB_GB_MB(string v)
        {
            string result;
            long Value = (long)Convert.ToDouble(v);
            if (Value.ToString().Length > 12) // Reformat if TB
            {
                result = Convert.ToInt64(Value / 1024 / 1024 / 1024 / 1024).ToString();
                return String.Format("{0} TB", result);
            }
            else if (Value.ToString().Length > 9) // Reformat if GB
            {
                result = Convert.ToInt64(Value / 1024 / 1024 / 1024).ToString();
                return String.Format("{0} GB", result);
            }
            else if (Value.ToString().Length > 6)  // Reformat if MB
            {
                result = Convert.ToInt64(Value / 1024 / 1024).ToString();
                return String.Format("{0} MB", result);
            }
            else if (Value.ToString().Length > 3)  // Reformat if KB
            {
                result = Convert.ToInt64(Value / 1024).ToString();
                return String.Format("{0} KB", result);
            }


            return String.Format("###,###,###,###,##0.00 Bytes", Value.ToString());
        }
		#endregion

		#region TaskService
		public static void setTaskService()
		{
			//using (var wic = ServiceSecurityContext.Current.WindowsIdentity.Impersonate())
			//{
				using (TaskService ts = new TaskService())
				{
					// Get the company name 
					FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
					string companyName = versionInfo.CompanyName;
					string appName = GetAppTitle();
					// Set the program path
					string folderPath = string.Format(@"{0}\{1}", Environment.SpecialFolder.ProgramFiles.ToString(), companyName);
					string programPath = string.Format(@"{0}\ActiveReport.exe", folderPath);

					// Create a new task definition and assign properties
					TaskDefinition td = ts.NewTask();
					td.RegistrationInfo.Description = String.Concat("The ", appName, " Client does SQL queries against all SQL Server in list.");

					// Set trigger and action and other properties...
					td.Principal.RunLevel = TaskRunLevel.Highest;

					// Create a trigger that will fire at the end of the week, every week
					td.Triggers.Add(new WeeklyTrigger { DaysOfWeek = DaysOfTheWeek.Saturday });

					// Create an action that will launch the program whenever the trigger fires
					td.Actions.Add(new ExecAction(programPath, "'-d 0' '-m 1'", null));

					ts.RootFolder.RegisterTaskDefinition(String.Concat(companyName," - ", appName, " Client"), td);
				}
			//}

		}
		#endregion
	}
}
