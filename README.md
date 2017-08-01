# Active Report
SQL Server Active Report 3.0

Consul application tool wirthen in c#.
This tool help to get SQL Server metadata info into single XML file per server.

## Problams and Solutions

### SQL Files:
* Etch user can add T-SQL script as match as he like to the SQL Script Path.
* The application knows automatically to build the XML elements.
* The script need to run(without any errors) on SQL Server 2005+(Linux or Windows).
* If the script output have Multiple Active Result Sets([MARS](https://docs.microsoft.com/en-us/sql/relational-databases/native-client/features/using-multiple-active-result-sets-mars)). The last result set will be taken to the XML.
* If one or more columns from the result sets have no name(title) It will generate one for you.
* You can rest assure that you do not need to activate [xp_cmdshell](https://docs.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/xp-cmdshell-transact-sql) (Windows OS only) per single script.
* If you intened to write your own script please look at the scipt structure in the folder and try that they will look alike.
* On etch script the application adds a prefix - 
```sql
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET ARITHABORT ON;
SET LOCK_TIMEOUT 2000;
SET DEADLOCK_PRIORITY LOW;
```

### Performance
* By default the etch SQL script run simultaneously on etch server(Multi Thread).
* By default the process run simultaneously(Multi Thread) on all server that in the list.


### Parameters
* m, "multithreading"- Activate multithreading when running queries. DefaultValue = true.
* d, "debug"- Prints all messages to standard output. DefaultValue = false.
* t, "taskScheduler"- Set task scheduler to work once a week. DefaultValue = false.



### To Do:
* Add option to add application to Windows scheduled task.
* Add section that can handle zip files. - Done
* Add section that can handle New/Updated SQL files from GitHub.
* Add section that can use ftp for transfer the file to the server.
* Add Event Viewer for XML.
* Remove from sub root in xml Urgent_Backup => xmlns="""
