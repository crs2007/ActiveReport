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
using System.Xml;

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
		private static Boolean _useFTP = true;
		public static string fileErrorPathBin;
		public static string fileErrorPathOut;
		public static readonly int _durationLimit = 3;
		public static List<string> OutputFiles = new List<string>();
		public static readonly string appErrorFile = "AppError.txt";
		public static string outputPath = @"C:\Output\";
		public static string FTPUserName;
		public static string FTPPassword;
        public static bool logOutWrite = false;
		#endregion
		static void Main(string[] args)
		{
			#region Parameters
			string errorDetail = String.Empty;
			string destPath = outputPath;
            if (ConfigurationHandler.GetConfigValue("LogFileOut") == "YES")
                logOutWrite = true;
			var options = new Options();
			string toDo = @"To Do List:
";
			toDo += ConfigurationHandler.readFile(@"ToDo.txt");
			try
			{
				outputPath = ConfigurationHandler.GetConfigValue("XMLOutputPath");
			}
			catch (Exception)
			{

				outputPath = @"C:\Output\";
			}

			fileErrorPathOut = outputPath + appErrorFile;
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
                insertAppErrors("Files", String.Concat("Error when delete AppError.txt file from - ", fileErrorPathBin,". ",ex.Message), 0, String.Empty, true);
				goto EndOfApp;
			}
			#endregion

			#region Get FTP user and passwords
            try
            {
			    RijndaelCrypt EncriptC = new RijndaelCrypt("xxx");
			    FTPUserName = EncriptC.Decrypt(ConfigurationHandler.GetConfigValue("FTPUserName"));
			    FTPPassword = EncriptC.Decrypt(ConfigurationHandler.GetConfigValue("FTPPassword"));
            }
            catch (Exception ex)
            {
                insertAppErrors("ftp", ex.Message, 0, String.Empty, false);
            }
			#endregion

			#region Parse Arguments
			if (CommandLine.Parser.Default.ParseArguments(args, options))
			{
				if (options.debug) _debug = true;
				if (options.github)
				{
					try
					{
						ConfigurationHandler.downloadFileFromGitHub();
					}
					catch (Exception ex)
					{
                        insertAppErrors("GitHub", "Error when updating SQL file form the GitHub repositorie.", 0, String.Empty, false);
					}
				}
				if (!options.ftp) _useFTP = false;
				if (options.multithreading) _useThread = false;
				if (options.taskScheduler)
				{
					_debug = true;
					try
					{
						ConfigurationHandler.setTaskService();
						Console.ForegroundColor = ConsoleColor.Green;
						printToScreen(String.Empty, "Task Scheduler has been created successfully!");
						Console.ForegroundColor = ConsoleColor.White;
					}
					catch (Exception ex)
					{
						if (ex.Message.Contains("Access is denied"))
						{
							printToScreen(String.Empty, String.Empty, "Please, open the app in Administrator mode and try again.");                            
                            insertAppErrors("Schedule Task", "Error when tring create Task Schedule. Please, open the app in Administrator mode and try again.", 0, String.Empty, false);
						}
						else
						{
                            insertAppErrors("Schedule Task", "Error when tring create Task Schedule.", 0, String.Empty, true);
						}
					}


					// Don't let the program perform any other functions that aren't needed
					goto EndOfApp;
				}
			}
			#endregion
			
			writePublicFile(String.Concat("******              Runing ", ConfigurationHandler.GetAppTitle(), " at ", DateTime.Now.ToString(), "              ******"));

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
					), string.Format("Parallelism:: {0}",
					(_useThread) ? "Multi-Thread" : "Single-Thread"
					), "\nOutput files can be found - ", outputPath, "\n", toDo, "\n\n");
				Console.WriteLine(AboutText);
				writePublicToAppendFile(AboutText);

			}
			#endregion
			string tempForErrorServerName = String.Empty;
			try
			{
				ConfigurationHandler.createDir(outputPath);
				ConfigurationHandler.ClearFolder(outputPath);
				List<string> connectionStrings = ConfigurationHandler.getConectionString();
				List<SQLScript> queries = ConfigurationHandler.getSQLFileContent();
				List<Task> list = new List<Task>();

				foreach (string connectionString in connectionStrings)
				{
					try
					{
						tempForErrorServerName = ConfigurationHandler.getServerNameFromConnectionString(connectionString);
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
				insertAppErrors("General", errorDetail,0, tempForErrorServerName, true);

			}


			#region Zip & ftp
            try
            {
                if (_useFTP) ConfigurationHandler.uploadFileWithFTP(OutputFiles, FTPUserName, FTPPassword);
            }
            catch (Exception e)
            {
                insertAppErrors("ftp", e.Message, 0, String.Empty, true);
            }

			try
			{
                string _ZipFile = outputPath + DateTime.Now.ToString("yyyyMMdd") + "-Report-" + ConfigurationHandler.GetClientName() + ".zip";
                if (ConfigurationHandler.GetConfigValue("Compress") == "YES")
				    ConfigurationHandler.CreateZipFile(_ZipFile, OutputFiles);
			}
			catch (Exception e)
			{
                insertAppErrors("Zip", e.Message, 0, String.Empty, true);
			}
			#endregion

			EndOfApp:

			//if (AppErrors.Count(p => p.ServerName == String.Empty) > 0)
			//{
				errorDetail = String.Empty;
				string totalErrorDetail = String.Empty;
                string GeneralErrorDetail = @"General Errors::\n
----------------------------------------";
				foreach (AppError errItem in AppErrors)
				{
					if (errItem.ServerName == String.Empty)
					{
                        GeneralErrorDetail += Environment.NewLine + errItem.Error.Message.ToString();

						if (errItem.PrintOnScreen)
							errorDetail += errItem.Error.Message.ToString();
					}
				}
                totalErrorDetail += Environment.NewLine + GeneralErrorDetail;
				if (_debug)
				{

					if (errorDetail != String.Empty)
					{
						printToScreen(String.Empty, String.Empty, errorDetail);
					}
				}
				writePublicToAppendFile(totalErrorDetail.Replace("\n", Environment.NewLine));

				OutputFiles.Add(fileErrorPathOut);
			//}
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
                        _Error += errItem.Error.ToString().Replace("System.Exception: " + serverName + ".", "") + Environment.NewLine;

				}
				_Error = String.Concat(Environment.NewLine, serverName, "::", Environment.NewLine, "----------------------------------------", Environment.NewLine, _Error);
				writePublicToAppendFile(_Error.Replace("\n", Environment.NewLine));
			}


		}

		public static void writePublicFile(string Content)
		{
            try
            {
                if (logOutWrite) 
                    writeFile(fileErrorPathOut, Content);
                else
                    writeFile(fileErrorPathBin, Content);
            }
            catch (Exception)
            {
                writeFile(fileErrorPathBin, Content);
            } 
			
		}

		public static void writePublicToAppendFile(string Content)
		{
            try
            {
                if (logOutWrite)
                    writeToAppendFile(fileErrorPathOut, Content);
                else
                    writeToAppendFile(fileErrorPathBin, Content);     
            }
            catch (Exception)
            {
			    writeToAppendFile(fileErrorPathBin, Content);
            }

		}

		private static void writeFile(string FullFilePath, string Content)
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

		private static void insertDebugError(string Subject, string Error, int Duration, string ServerName)
		{

			DebugErrors.Add(new DebugError() { Subject = Subject, Error = (string.IsNullOrEmpty(Error) && Duration > _durationLimit) ? String.Format("Timeout occurred. waiting for {0} seconds.", Duration) : Error, Duration = Duration, ServerName = ServerName });

		}

		private static void insertAppErrors(string Subject, string Error, int Duration, string ServerName, Boolean PrintOnScreen)
		{

            AppErrors.Add(new AppError() { Error = new Exception(String.Concat(((ServerName == String.Empty) ? "" : ServerName + "."), (Subject == String.Empty) ? "" : Subject  + ": ", (string.IsNullOrEmpty(Error) && Duration > _durationLimit) ? String.Concat("Query run successfully! but execution has been taking too long(", Duration.ToString(), " sec)") : Error)), ServerName = ServerName, PrintOnScreen = PrintOnScreen });

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

		private static void printToScreen(string serverName, string Erea, Boolean IsStart)
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
			Boolean _isLinuxOS = true;

			XNamespace xsiNs = ConfigurationHandler.GetXsiNs();
			connectionStringServerName = ConfigurationHandler.getServerNameFromConnectionString(connectionString);

			XmlWork xmlWork = new XmlWork(connectionString, serverName, debug, useThread);
			List<Task> list = new List<Task>();
			_isLinuxOS = ConfigurationHandler.getIsLinuxOS(connectionString);
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

			#region Disk Information
			_subject = "DiskInfo";
			Error = String.Empty;
			StartTime = DateTime.Now;
			try
			{
				XElement xEleDiskInfo;
				printToScreen(serverName, _subject, true);
				if (_isLinuxOS)
				{
					xEleDiskInfo = ConfigurationHandler.getVolumeInfoLinux(serverName);
					xml.AddFirst(xEleDiskInfo);
				}
				else
				{
					xEleDiskInfo = ConfigurationHandler.getVolumeInfo(serverName);
					xml.AddFirst(xEleDiskInfo);
				}

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
				insertDebugError(_subject, Error, Duration, serverName);
				insertAppErrors(_subject, Error, Duration, serverName, false);
			}
			#endregion

			#region Event Viewer
			_subject = "EventViewer";
			Error = String.Empty;
			StartTime = DateTime.Now;
			try
			{
				XElement xEleEventViewer;
				printToScreen(serverName, _subject, true);
				if (_isLinuxOS)
				{
					//TODO
				}
				else
				{
					xEleEventViewer = ConfigurationHandler.getEventViewerInfo(serverName);
					xml.AddFirst(xEleEventViewer);
				}

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
				insertDebugError(_subject, Error, Duration, serverName);
				insertAppErrors(_subject, Error, Duration, serverName, false);
			}
			#endregion

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
				insertDebugError(_subject, Error, Duration, serverName);
				insertAppErrors(_subject, Error, Duration, serverName, false);
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
				insertDebugError(_subject, Error, Duration, serverName);
				insertAppErrors(_subject, Error, Duration, serverName, false);
			}
			#endregion

			#region SQL Backup
			_subject = "UrgentBackup";
			Error = String.Empty;
			StartTime = DateTime.Now;
			try
			{
				printToScreen(serverName, _subject, true);
				var xEleSQLServerBakup = ConfigurationHandler.getSQLServerBackup(connectionString, _subject);
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
				insertDebugError(_subject, Error, Duration, serverName);
				insertAppErrors(_subject, Error, Duration, serverName, false);
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
				insertDebugError(_subject, Error, Duration, serverName);
				insertAppErrors(_subject, Error, Duration, serverName, false);
			}
			#endregion

			#region DebugError
			_subject = "DebugError";
			var xEleDebugErrors = new XElement(_subject,
		from DebugError in DebugErrors
		where DebugError.ServerName == serverName
		select new XElement(_subject + "_Data",
						 new XAttribute(XNamespace.Xmlns + "xsi",
							  "http://www.w3.org/2001/XMLSchema-instance"),
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
            string tempInnerXML = String.Empty;
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

					result = dynamicResult.ToXml(query.FileName, query.FileName + "_Data");
					if (dynamicResult.Count() > 0)
					{
						XElement rootXML = new XElement(query.FileName);
						foreach (XElement innerXML in result.Elements(query.FileName + "_Data"))
						{
							innerXML.Add(new XAttribute(XNamespace.Xmlns + "xsi",
							  "http://www.w3.org/2001/XMLSchema-instance"));
                            tempInnerXML = innerXML.ToString().Replace(">true<", ">1<").Replace(">false<", ">0<");

                            rootXML.Add(XElement.Parse(tempInnerXML));
						}
						result = rootXML;
					}
					Duration = (DateTime.Now - StartTime).Seconds;

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
				else 
                { 
                    _connectionSafe = false;
                    writeErrorsOnServerToFile(_serverName);
                }
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
                    insertAppErrors(String.Empty, serverNameError.Message.ToString(), 0, (_serverName == String.Empty) ? ConfigurationHandler.getServerNameFromConnectionString(_connectionString) : _serverName, false);
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
						throw new Exception(String.Concat("\nError when changing server config values"), ex);
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
							error = String.Concat("\n", (_serverName == ".") ? Environment.MachineName : _serverName, ":: Error when changing server config ", attribute);
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
				_element = new XElement("Report",
						 new XAttribute(XNamespace.Xmlns + "xsi",
							  "http://www.w3.org/2001/XMLSchema-instance"));
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
			[Option('m', "multithreading", DefaultValue = false,
			  HelpText = "Activate multithreading when running queries.")]
			public bool multithreading { get; set; }

			[Option('d', "debug", DefaultValue = false,
			  HelpText = "Prints all messages to standard output.")]
			public bool debug { get; set; }

			[Option('g', "github", DefaultValue = false,
			  HelpText = "Update all sql files from GitHub repositorie.")]
			public bool github { get; set; }

			[Option('t', "taskScheduler", DefaultValue = false,
			  HelpText = "Set task scheduler to work once a week.")]
			public bool taskScheduler { get; set; }

			[Option('f', "ftp", DefaultValue = false,
			  HelpText = "Use ftp to upload the files.")]
			public bool ftp { get; set; }

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
