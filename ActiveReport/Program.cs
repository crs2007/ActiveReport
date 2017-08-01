using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ActiveReport.Class;
using System.Xml.Linq;
using System.Data.SqlClient;
using System.IO;
using Dapper;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Data.Common;
using System.Data;
using System.Threading;
using CommandLine;
using CommandLine.Text;

namespace ActiveReport
{
    public class Program
	{
		#region Parameters
		public static List<AppError> AppErrors = new List<AppError>();
        public static List<DebugError> DebugErrors = new List<DebugError>();
        public static string SQLPrefix = ConfigurationHandler.GetSQLPrefix();
        private static Boolean _debug = false;
        private static Boolean _useThread = true;
        public static string fileErrorPathBin;
        public static string fileErrorPathOut;
		public static readonly int _durationLimit = 3;
        public static List<string> OutputFiles = new List<string>();
        public static readonly string appErrorFile = "AppError.txt";
        public static string outputPath = @"C:\Output\";
		#endregion
		static void Main(string[] args)
        {
            #region Parameters
			string errorDetail = String.Empty;
            string destPath = outputPath;
			var options = new Options();
            string toDo = @"To Do List:
* Add option to add application to Windows scheduled task.
* Add section that can handle zip files. - Done
* Add section that can handle New/Updated SQL files from GitHub.
* Add section that can use ftp for transfer the file to the server.
* Add Event Viewer for XML.
* Remove from sub root in xml Urgent_Backup => xmlns=""";
			#endregion


			#region Clean folder from AppError.txt
			destPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"AppError.txt");
            fileErrorPathBin = destPath;
			try
			{
                ConfigurationHandler.DeleteFile(fileErrorPathBin);
			}
			catch (Exception ex)
			{
                AppErrors.Add(new AppError() { Error = new Exception(String.Concat("Error when delete AppError.txt file from - ", fileErrorPathBin), ex), ServerName = String.Empty , PrintOnScreen = true });
				goto EndOfApp;
			}
			#endregion

			if (CommandLine.Parser.Default.ParseArguments(args, options))
			{
				if (options.debug) _debug = true;
				if (!options.multithreading) _useThread = false;
				if (options.taskScheduler)
				{
					try
					{
						ConfigurationHandler.setTaskService();
					}
					catch (Exception ex)
					{

                        AppErrors.Add(new AppError() { Error = new Exception("Error when tring create Task Schedule.", ex), ServerName = String.Empty, PrintOnScreen = true });
					}
					

					// Don't let the program perform any other functions that aren't needed
					goto EndOfApp;
				}
			}
            try
            {
                outputPath = ConfigurationHandler.GetConfigValue("XMLOutputPath");
            }
            catch (Exception)
            {
                
                outputPath = @"C:\Output\";
            }
            fileErrorPathOut = outputPath + appErrorFile;
            writePublicFile(String.Concat("******              Runing Active Report 3.0 at ", DateTime.Now.ToString(), "              ******"));
			#region Debug
			if (_debug)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Out.NewLine = String.Empty;

                Assembly execAssembly = Assembly.GetCallingAssembly();

                AssemblyName name = execAssembly.GetName();
                var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
                string AboutText;
                AboutText = String.Concat(string.Format("{0}{1} for SQL Server health check\nMade by {2}\nCopyrights: {3}\n",
                    Environment.NewLine,
                    ConfigurationHandler.GetAppTitle(),
                    versionInfo.CompanyName,
                    versionInfo.LegalCopyright
                    ),string.Format("Parallelism:: {0}",
                    (_useThread) ? "Multi-Thread" : "Single-Thread"
                    ), "\nOutput files can be found - ", outputPath, "\n",toDo,"\n\n");
                Console.WriteLine(AboutText);
                writePublicToAppendFile(AboutText);

            }
			#endregion


            try
            {
                ConfigurationHandler.downloadFileFromGitHub();
            }
            catch (Exception ex)
            {
                
                AppErrors.Add(new AppError() { Error = new Exception("Error when updating SQL file form the web.", ex), ServerName = String.Empty, PrintOnScreen = false });
			}
			try
			{

				
				ConfigurationHandler.createDir(outputPath);
                
                List<string> connectionStrings = ConfigurationHandler.getConectionString();

				List<SQLScript> queries = ConfigurationHandler.getSQLFileContent();

				List<Task> list = new List<Task>();

				foreach (string connectionString in connectionStrings)
				{

                    try
                    {
                        #region Change SQL Server configuration like xp_cmdshell
                        SQLServer SQLServerWork = new SQLServer(connectionString, queries, outputPath, _debug, _useThread);
					    #endregion
                        if (SQLServerWork.isConnectionSafe())
                        {
                            if (_useThread)
                            {
                                Task task = new Task(() => SQLServerWork.Run());
                                list.Add(task);
                                task.Start();
                            }
                            else
                            {
                                SQLServerWork.Run();
                            }
                        }
                        else
                        {
                            continue;
                        }
                        
                    }
                    catch (Exception)
                    {
                        
                        throw;
                    }

					

                }
                if (_useThread) Task.WaitAll(list.ToArray());
				
			}
			catch (Exception e)
			{
				var s = new StackTrace(e);
				var thisasm = Assembly.GetExecutingAssembly();
				var methodname = s.GetFrames().Select(f => f.GetMethod()).First(m => m.Module.Assembly == thisasm).Name;
                errorDetail = String.Concat("Method Name:", methodname.ToString(), Environment.NewLine, e.ToString());

                AppErrors.Add(new AppError() { Error = new Exception(errorDetail), ServerName = String.Empty, PrintOnScreen = true });
				
			}
EndOfApp:
            if (AppErrors.Count(p => p.ServerName == String.Empty) > 0)
			{
				errorDetail = String.Empty;
                string totalErrorDetail = String.Empty;
                foreach (AppError errItem in AppErrors)
                {
                    if (errItem.ServerName == String.Empty)
                    {
                        totalErrorDetail += errItem.Error.ToString() + Environment.NewLine;

                        if (errItem.PrintOnScreen)
                            errorDetail += errItem.Error.ToString();
                    }
				}

				if (_debug)
				{

					if (errorDetail != String.Empty)
					{
                        printToScreen(String.Empty, String.Empty, errorDetail);
					}
				}
                writePublicToAppendFile(totalErrorDetail.Replace("\n",Environment.NewLine));

                OutputFiles.Add(fileErrorPathOut);
			}


            ConfigurationHandler.CreateZipFile(outputPath + DateTime.Now.ToString("yyyyMMdd") + "-Report-" + ConfigurationHandler.GetClientName() + ".zip", OutputFiles);
            if (_debug)
            {
                Console.WriteLine("\nPress enter to close...");
                Console.ReadLine();
            }
        }

        public static void writeErrorsOnServerToFile(string serverName)
        {
            string _Error = String.Empty;
            if (AppErrors.Count(p => p.ServerName == serverName) > 0)
            {
                foreach (AppError errItem in AppErrors)
                {
                    if (errItem.ServerName == serverName)
                    _Error += errItem.Error.ToString() + Environment.NewLine;

                }
                _Error = String.Concat(Environment.NewLine, serverName, "::", Environment.NewLine, "----------------------------------------", Environment.NewLine, _Error);
                writePublicToAppendFile(_Error.Replace("\n", Environment.NewLine));
            }


        }
			
        public static void writePublicFile(string Content)
        {
            writeFile(fileErrorPathOut, Content);
            writeFile(fileErrorPathBin, Content);
        }

        public static void writePublicToAppendFile(string Content)
        {
            writeToAppendFile(fileErrorPathOut, Content);
            writeToAppendFile(fileErrorPathBin, Content);
            
        }

        private static void writeFile(string FullFilePath,string Content)
        {
            if (File.Exists(FullFilePath))
            {
                File.Delete(FullFilePath);
            }
            File.WriteAllText(FullFilePath, Content);
        }

        private static void writeToAppendFile(string FullFilePath, string Content)
        {
            Content = Content.Replace("\n", Environment.NewLine);
            if (File.Exists(FullFilePath))
            {
                File.AppendAllText(FullFilePath, Content);
            }
            else writeFile(FullFilePath, Content);
            
        }        

        private static void insertDebugError(string Subject,string Error, int Duration, string ServerName)
        {

            DebugErrors.Add(new DebugError() { Subject = Subject, Error = (string.IsNullOrEmpty(Error) && Duration > _durationLimit) ? String.Format("Timeout occurred. waiting for {0} seconds.", Duration) : Error, Duration = Duration, ServerName = ServerName });

        }

        private static void insertAppErrors(string Subject, string Error, int Duration, string ServerName, Boolean PrintOnScreen)
        {

            AppErrors.Add(new AppError() { Error = new Exception(String.Concat(ServerName, ".", Subject, ": ", (string.IsNullOrEmpty(Error) && Duration > _durationLimit) ? String.Concat("Query run successfully! but execution has been taking too long(", Duration.ToString(), " sec)") : Error)), ServerName = ServerName, PrintOnScreen = PrintOnScreen });

        }

        

        #region Print To Screen
        private static void printToScreen(string serverName, string Erea, string Error)
        {
            if (_debug)
            {
				serverName = (serverName == ".") ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : serverName;
                Console.ForegroundColor = ConsoleColor.Red;
                if (_useThread && !String.IsNullOrEmpty(serverName) && !String.IsNullOrEmpty(Erea))
                {
                    Console.WriteLine("\n{0}::Running script {1} - \n{2}", serverName, Erea, Error);
                }
                else
                {
                    Console.WriteLine("\n" + Error);
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static void printToScreen(string serverName, string message)
        {
            if (_debug)
            {
				serverName = (serverName == ".") ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : serverName;
				Console.WriteLine("\n{0}:: {1}\n", serverName, message);
            }
        }

        private static void printToScreen(string serverName,string Erea,Boolean IsStart )
        {
            if (_debug)
            {
				serverName = (serverName == ".") ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : serverName;
				if (IsStart)
                {
                    if (_useThread)
                    {
                        Console.WriteLine("\n{0}::Running Script {1}...", serverName, Erea);
                    }
                    else
                    {
                        Console.WriteLine("\nRunning script {0}...", Erea);
                    }
                    return;
                }
                if (!IsStart)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    if (_useThread)
                    {
                        Console.WriteLine("\n{0}::Script {1} - Run Successfully!", serverName, Erea);
                    }
                    else
                    {
                        Console.WriteLine(" - Run Successfully!");
                    }
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
        #endregion

        private static void getInfoFromServer(string connectionString, List<SQLScript> queries, string outputPath, Boolean debug, Boolean useThread, string serverName)
        {

            string FullFilePath;
            DateTime StartTime;
            int Duration;
            string Error;
            string _subject;
            string connectionStringServerName = String.Empty;

			XNamespace xsiNs = ConfigurationHandler.GetXsiNs();
            connectionStringServerName = ConfigurationHandler.getServerNameFromConnectionString(connectionString);
            
            XmlWork xmlWork = new XmlWork(connectionString, serverName, debug, useThread);
            List<Task> list = new List<Task>();

            foreach (SQLScript query in queries)
            {
                try
                {
                    if (useThread)
                    {
                        Task task = new Task(() => xmlWork.Run(query));
                        list.Add(task);
                        task.Start();
                    }
                    else
                    {
                        xmlWork.Run(query);
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }

			Task.WaitAll(list.ToArray());

			XElement xml = xmlWork.GetElement();

            #region Nodes Info
            //Nodes Info
            _subject = "Nodes";
            Error = String.Empty;
            StartTime = DateTime.Now;
            try
            {
                printToScreen(serverName, _subject, true);
                var xEleSQLClusterNode = ConfigurationHandler.getNodesInfo(connectionString);
                xml.AddFirst(xEleSQLClusterNode);
                printToScreen(serverName, _subject, false);
            }
            catch (Exception se)
            {
                Error = se.Message.ToString();
                printToScreen(serverName, _subject, Error);
            }
            Duration = (DateTime.Now - StartTime).Seconds;
            if ((Duration > _durationLimit) || (!string.IsNullOrEmpty(Error)))
            {
                insertDebugError(_subject,Error , Duration, serverName);
                insertAppErrors(_subject,Error, Duration, serverName, false);
            }
            #endregion

            #region Login Info
            _subject = "InvalidLogin";
            Error = String.Empty;
            StartTime = DateTime.Now;
            try
            {
                printToScreen(serverName, _subject, true);
                var xEleSQLInvalidLogin = ConfigurationHandler.getUnevenLogins(connectionString, serverName);
                xml.AddFirst(xEleSQLInvalidLogin);
                printToScreen(serverName, _subject, false);
            }
            catch (Exception se)
            {
                Error = se.Message.ToString();
                printToScreen(serverName, _subject, Error);
            }
            Duration = (DateTime.Now - StartTime).Seconds;
            if ((Duration > _durationLimit) || (!string.IsNullOrEmpty(Error)))
            {
                insertDebugError(_subject,Error, Duration,serverName);
                insertAppErrors(_subject,Error , Duration, serverName, false);
            }
            #endregion

            #region SQL Backup
            _subject = "UrgentBackup";
            Error = String.Empty;
            StartTime = DateTime.Now;
            try
            {
                printToScreen(serverName, _subject, true);
                var xEleSQLServerBakup = ConfigurationHandler.getSQLServerBackup(connectionString);
                xml.AddFirst(xEleSQLServerBakup);
                printToScreen(serverName, _subject, false);
            }
            catch (Exception se)
            {
                Error = se.Message.ToString();
                printToScreen(serverName, _subject, Error);
            }
            Duration = (DateTime.Now - StartTime).Seconds;
            if ((Duration > _durationLimit) || (!string.IsNullOrEmpty(Error)))
            {
                insertDebugError(_subject,Error , Duration, serverName);
                insertAppErrors(_subject,Error , Duration, serverName, false);
            }
			#endregion

			#region SQL Error Log
			_subject = "ErrorLog";
			Error = String.Empty;
			StartTime = DateTime.Now;
			try
			{
				printToScreen(serverName, _subject, true);
				var xEleSQLServerError = ConfigurationHandler.getSQLServerError(connectionString);
				xml.AddFirst(xEleSQLServerError);
				printToScreen(serverName, _subject, false);
			}
			catch (Exception se)
			{
				Error = se.Message.ToString();
				printToScreen(serverName, _subject, Error);
			}
			Duration = (DateTime.Now - StartTime).Seconds;
			if ((Duration > _durationLimit) || (!string.IsNullOrEmpty(Error)))
			{
				insertDebugError(_subject,Error , Duration, serverName);
				insertAppErrors(_subject,Error , Duration, serverName, false);
			}
			#endregion

			#region DebugError
			var xEleDebugErrors = new XElement(xsiNs + "DebugError",
        from DebugError in DebugErrors
        where DebugError.ServerName == serverName
        select new XElement(xsiNs + "DebugError_Data",
                     new XElement("Subject", DebugError.Subject),
                       new XElement("Error", DebugError.Error),
                       new XElement("Duration", DebugError.Duration)
                   ));
            xml.AddFirst(xEleDebugErrors);
            #endregion
            string _fileName = DateTime.Now.ToString("yyyyMMdd") + "-Report-" + serverName.Replace(@"\", "@") + ".xml";
            FullFilePath = outputPath + _fileName;
            writeFile(FullFilePath, xml.ToString());
            OutputFiles.Add(FullFilePath);
        }

        private static XElement runSQLScript(string connectionString, SQLScript query, XNamespace xsiNs, string serverName, Boolean debug, Boolean useThread)
        {

            
            DateTime StartTime;
            int Duration;
            string Error;
            XElement result = null;
            int _MaxSQLQueryTimeout = ConfigurationHandler.GetSQLQueryTimeout();

            #region Run SQL Script File
            printToScreen(serverName, query.FileName, true);
            using (var conn = new SqlConnection(connectionString))
            {
                Error = String.Empty;
                conn.Open();
                Duration = 999;
                StartTime = DateTime.Now;
                IEnumerable<dynamic> dynamicResult = null;
                var p = new DynamicParameters();
                IDbTransaction transaction = null;
                try
                {
                    dynamicResult = conn.Query(SQLPrefix + query.Content, p, transaction, buffered: false, commandTimeout: _MaxSQLQueryTimeout, commandType: CommandType.Text);


                    var resultXml = dynamicResult.ToXml(query.FileName, xsiNs + query.FileName + "_Data");
                    Duration = (DateTime.Now - StartTime).Seconds;

                    result = resultXml;
                    printToScreen(serverName, query.FileName, false);

                }
                catch (SqlException se)
                {
                    if (Duration == 999) Duration = (DateTime.Now - StartTime).Seconds;
                    if (se.Number == -2)
                    {
                        Error = String.Format("Timeout occurred. waiting for {0} seconds.", Duration);
                    }
                    else Error = se.Message.ToString();
                    printToScreen(serverName, query.FileName, Error);
                }

                if ((Duration > 15) || (!string.IsNullOrEmpty(Error)))
                {
                    insertDebugError(query.FileName, Error, Duration, serverName);
                    //insertAppErrors(_subject, Error, Duration, serverName, false);
                    AppErrors.Add(new AppError() { Error = new Exception(String.Concat(serverName, ".", query.FileName, ": ", (string.IsNullOrEmpty(Error)) ? String.Concat("Query run successfully! but execution has been taking too long(", Duration.ToString(), " sec)") : Error)), ServerName = serverName, PrintOnScreen = false });
                }

                return result;
            }

			#endregion


		}
		
		#region Calsses
		class SQLServer
		{
			#region Parameters
			static readonly object _lock = new object();
			Boolean _xp_cmdshell;
			Boolean _showAdvancedOptions;
			Boolean _debug;
			Boolean _useThread;
			string _connectionString;
			List<SQLScript> _queries;
			string _outputPath;
			string _serverName;
            Boolean _connectionSafe;
			#endregion


            public SQLServer(string connectionString, List<SQLScript> queries, string outputPath, Boolean debug, Boolean useThread)
			{
				object _lock = new object();
				_debug = debug;
				_useThread = useThread;
				_outputPath = outputPath;
				_queries = queries;
				_connectionString = connectionString;
				_serverName = ConfigurationHandler.getServerNameFromConnectionString(_connectionString);
                if (getServerNamebySQLServerConnection())
                {
                    _connectionSafe = true;
                    getSQLServerSpconfigureState();
                    if (!_xp_cmdshell)
                        set_xp_cmdshell(true);
                }
                else { _connectionSafe = false; }
			}

            public Boolean isConnectionSafe()
            {
                return _connectionSafe;
            }

            private Boolean getServerNamebySQLServerConnection()
            {
                #region checkSQLServerConnection
                try
                {
                    if (_debug)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("\n\nConnecting to server ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("{0}", (_serverName == ".") ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : _serverName);
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("...");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    _serverName = ConfigurationHandler.checkSQLServerConnection(_connectionString, SQLPrefix);
                    if (_debug)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine("\nConnection to server ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("{0} ", (_serverName == ".") ? System.Environment.GetEnvironmentVariable("COMPUTERNAME") : _serverName);
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine("succeeded!");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                    return true;
                }
                catch (Exception serverNameError)
                {
                    AppErrors.Add(new AppError() { Error = new Exception(serverNameError.Message.ToString()), ServerName = _serverName, PrintOnScreen = false });
					printToScreen(_serverName, "checkSQLServerConnection", serverNameError.Message.ToString());
                    return false;
                }
                #endregion
            }

			private void getSQLServerSpconfigureState()
			{
				string _ConfigValue;
				using (var conn = new SqlConnection(_connectionString))
				{

					try
					{
						conn.Open();
						var cmd = conn.CreateCommand();
						cmd.CommandText = String.Concat(SQLPrefix, @"EXEC sp_configure 'show advanced options';");
						_ConfigValue = cmd.ExecuteScalar().ToString();
						if (_ConfigValue == "1")
						{
							_showAdvancedOptions = true;
						}
						else
						{
							_showAdvancedOptions = false;
						}
						cmd.CommandText = String.Concat(SQLPrefix, @"SELECT value_in_use FROM sys.configurations WHERE name = 'xp_cmdshell';");
						_ConfigValue = cmd.ExecuteScalar().ToString();
						if (_ConfigValue == "1")
						{
							_xp_cmdshell = true;
						}
						else
						{
							_xp_cmdshell = false;
						}
						cmd.Dispose();
					}
					catch (Exception ex)
					{
						throw new Exception(String.Concat("\nError when changing server config values"),ex);
					}
				}
			}

			public void Run()
			{

                Program.getInfoFromServer(_connectionString, _queries, _outputPath, _debug, _useThread, _serverName);

				lock (_lock)
				{
					set_xp_cmdshell(false);
                    printToScreen(_serverName, "**** Has finised running scripts ****");

                    writeErrorsOnServerToFile(_serverName);
				}
			}

			public void set_xp_cmdshell(Boolean turnOn)
			{
				if (turnOn)
					set_xp_cmdshell();
				else
				{
					if (!_xp_cmdshell)
						return;
					else
					{
						connectSQLServer(false, "xp_cmdshell");
					}
				}
			}

			private void set_xp_cmdshell()
			{
				if (_xp_cmdshell)
					return;
				else
				{
					set_ShowAdvancedOptions();
					connectSQLServer(true, "xp_cmdshell");
				}
			}

			private void set_ShowAdvancedOptions()
			{
				if (_showAdvancedOptions)
					return;
				else
				{
					connectSQLServer(true, "show advanced options");
				}
			}

			private void connectSQLServer(Boolean turnOn, string attribute)
			{

				string error = String.Empty;
				string _turnOn;

				if (turnOn)
					_turnOn = "1";
				else
					_turnOn = "0";
				#region runSQLServer_sp_configure
				try
				{

					using (var conn = new SqlConnection(_connectionString))
					{

						try
						{
							conn.Open();
							var cmd = conn.CreateCommand();
							cmd.CommandText = String.Concat(SQLPrefix, @"EXEC sp_configure '", attribute, @"', ", _turnOn, @";
RECONFIGURE WITH OVERRIDE;");
							cmd.ExecuteScalar();
							cmd.Dispose();
						}
						catch (Exception)
						{
							error = String.Concat("\n", (_serverName==".") ? Environment.MachineName : _serverName, ":: Error when changing server config ", attribute);
							throw new Exception(error);
						}
					}
					if (_debug)
					{
						Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine(String.Concat("\n", (_serverName == ".") ? Environment.MachineName : _serverName, ":: changing server config ", attribute, " to ", _turnOn));

					}
				}
				catch (Exception serverError)
				{
                    AppErrors.Add(new AppError() { Error = new Exception(serverError.Message.ToString()), ServerName = _serverName, PrintOnScreen = false });
					if (_debug)
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine("\n", (_serverName == ".") ? Environment.MachineName : _serverName, ":: " + serverError.Message.ToString());
						Console.ForegroundColor = ConsoleColor.White;
					}
					return;
				}
				#endregion
			}
		}

		class XmlWork
		{
			#region Parameters
			readonly static object _lock = new object();
			XElement _element;

			string _connectionString;
			string _serverName;
			Boolean _debug;
			Boolean _useThread;
			XNamespace xsiNs = ConfigurationHandler.GetXsiNs();
			#endregion

			public XmlWork(string connectionString, string serverName, Boolean debug, Boolean useThread)
			{
				_element = new XElement(xsiNs + "Report");
				_connectionString = connectionString;
				_serverName = serverName;
				_debug = debug;
				_useThread = useThread;
			}

			public void Run(SQLScript query)
			{

				XElement element = Program.runSQLScript(_connectionString, query, xsiNs, _serverName, _debug, _useThread);

				lock (_lock)
				{
					_element.AddFirst(element);

				}
			}

			public XElement GetElement()
			{
				return _element;
			}
		}

		// Define a class to receive parsed values
		class Options
		{
			[Option('m', "multithreading", DefaultValue = true,
			  HelpText = "Activate multithreading when running queries.")]
			public bool multithreading { get; set; }

			[Option('d', "debug", DefaultValue = false,
			  HelpText = "Prints all messages to standard output.")]
			public bool debug { get; set; }

			[Option('t', "taskScheduler", DefaultValue = false,
			  HelpText = "Set task scheduler to work once a week.")]
			public bool taskScheduler { get; set; }
			[ParserState]
			public IParserState LastParserState { get; set; }

			[HelpOption]
			public string GetUsage()
			{
				return HelpText.AutoBuild(this,
				  (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
			}
		}
		#endregion
	}
}
