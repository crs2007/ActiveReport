# Active Report
SQL Server Active Report 3.0

Console application tool writhen in c#.
This tool help to get SQL Server metadata info into single XML file per server.

## Problems and Solutions

### SQL Files:
* Each user can add T-SQL script as match as he likes to the SQL Script Path.
* The application knows automatically to build the XML elements.
* The script needs to run(without any errors) on SQL Server 2005+(Linux or Windows).
* If the script output has Multiple Active Result Sets([MARS](https://docs.microsoft.com/en-us/sql/relational-databases/native-client/features/using-multiple-active-result-sets-mars)). The last result set will be taken to the XML.
* If one or more columns from the result sets have no name(title) It will generate one for you.
* You can rest assure that you do not need to activate [xp_cmdshell](https://docs.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/xp-cmdshell-transact-sql) (Windows OS only) per single script.
* If you intend to write your own script please look at the script structure in the folder and try that they will look alike.
* On each script the application adds a prefix - 
```sql
SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET ARITHABORT ON;
SET LOCK_TIMEOUT 2000;
SET DEADLOCK_PRIORITY LOW;
```

### Performance:
* By default the each SQL script run simultaneously on each server(Multi Thread).
* By default the process run simultaneously(Multi Thread) on all server that in the list.

### Parameters
* m, "multithreading"- Activate multithreading when running queries. DefaultValue = true.
* d, "debug"- Prints all messages to standard output. DefaultValue = false.
* t, "taskScheduler"- Set task scheduler to work once a week. DefaultValue = false.(Hav to run in Administrator mode)
* f, "ftp"- Use ftp to upload the files. DefaultValue = true.
* g, "GitHub"- Update all sql files from GitHub repositorie. DefaultValue = true.

### Example/Instructions:
To get information on screen when the application is running-
```
ActiveReport.exe -d "true" -m "true"
```
### Prerequisite:
* .NET 4.5.2([Web Installer](https://www.microsoft.com/en-us/download/details.aspx?id=42643)|[Offline Installer](https://www.microsoft.com/en-us/download/details.aspx?id=42642))
* Local administrator on each SQL Server that you monitor. That because "[Access is denied](https://social.msdn.microsoft.com/Forums/vstudio/en-US/6229334e-d5ef-4016-9e7e-1c8718be8d43/access-is-denied-exception-from-hresult-0x80070005-eaccessdenied-in-vbnet?forum=netfxbcl&prof=required)" error when try OS methuds.

### To Do:
* Add full support for Linux OS.
* Add section that can handle New SQL files from GitHub.

### License:
ActiveReport is licensed under the [MIT license](https://github.com/crs2007/ActiveReport/blob/master/LICENSE).

### Warranty:
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS 
OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY 
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
