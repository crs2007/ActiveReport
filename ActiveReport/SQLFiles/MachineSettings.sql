-- =============================================
-- Author:		Sharon Rimer
-- Create date: 11/05/2016
-- Updtae date: 01/07/2016 Sharon Add NumberOfInstaces
-- Description:	Get Machine Settings Information 
-- Prerequisite: none
-- Output:		#MachineSettings
-- =============================================
DECLARE @SQLSVRACC VARCHAR(50); 
DECLARE @SQLAGTACC VARCHAR(50);
DECLARE @LOGINMODE VARCHAR(50);
DECLARE @SystemManufacturer VARCHAR(20);
DECLARE @outputIdentity TABLE ([ID] [int] NOT NULL IDENTITY(1,1),line VARCHAR(255));
DECLARE @sql VARCHAR(4000);
DECLARE @OperatingSystem sysname;
DECLARE	@IsLinux BIT;
DECLARE @ProcessorNameString NVARCHAR(1024);
DECLARE @SystemModal VARCHAR(20);
IF OBJECT_ID('sys.dm_os_host_info') IS NOT NULL
BEGIN
	SELECT	@IsLinux = 1,
			@OperatingSystem = host_distribution + ' ' + host_release
	FROM	sys.dm_os_host_info
	WHERE host_platform = 'Linux';
	IF OBJECT_ID('sys.dm_linux_proc_cpuinfo') IS NOT NULL
	SELECT @ProcessorNameString = model_name FROM sys.dm_linux_proc_cpuinfo;
	--SELECT * FROM sys.dm_li
END
IF @IsLinux = 0
BEGIN
    --systeminfo - For OS & KB
	SET @sql = 'systeminfo';
       
	INSERT  @outputIdentity EXEC xp_cmdshell @sql;
              
    SELECT @OperatingSystem = LTRIM(REPLACE(O.line,'OS Name:',''))
    FROM   @outputIdentity O
    WHERE  O.line LIKE '%OS Name:%';

	EXEC master..xp_regread @rootkey = 'HKEY_LOCAL_MACHINE',
    @key = 'HARDWARE\DESCRIPTION\System\BIOS',
    @value_name = 'SystemProductName',
    @value = @SystemModal OUTPUT;
  
	EXEC master..xp_regread N'HKEY_LOCAL_MACHINE',
    N'HARDWARE\DESCRIPTION\System\CentralProcessor\0\',
    N'ProcessorNameString', @ProcessorNameString OUTPUT;

END

EXEC master..xp_regread @rootkey = 'HKEY_LOCAL_MACHINE',
    @key = 'HARDWARE\DESCRIPTION\System\BIOS',
    @value_name = 'SystemManufacturer',
    @value = @SystemManufacturer OUTPUT;
  



IF OBJECT_ID('tempdb..#reg') IS NOT NULL
    EXEC ('Drop table #reg');

CREATE TABLE #reg
    (
        keyname CHAR(200) ,
        value VARCHAR(1000)
    );

DECLARE @key VARCHAR(8000); -- Holds Registry Key Value

--Build Sql Server's full service name
DECLARE @SQLServiceName VARCHAR(8000);
SELECT  @SQLServiceName = @@servicename;
SET @SQLServiceName = CASE WHEN @@servicename = 'MSSQLSERVER'
                            THEN 'MSSQLSERVER'
                            ELSE 'MSSQL$' + @@servicename
                        END; 

SET @key = 'SYSTEM\CurrentControlSet\Services\'
    + @SQLServiceName;


--MSSQLSERVER Service Account
INSERT  INTO #reg
        EXEC master..xp_regread 'HKEY_LOCAL_MACHINE', @key,
            'ObjectName';
UPDATE  #reg
SET     keyname = @SQLServiceName; 

--SQLSERVERAGENT Service Account
DECLARE @AgentServiceName VARCHAR(8000);
SELECT  @AgentServiceName = @@servicename;
SET @AgentServiceName = CASE WHEN @@servicename = 'MSSQLSERVER'
                                THEN 'SQLSERVERAGENT'
                                ELSE 'SQLAgent$' + @@servicename
                        END; 

SET @key = 'SYSTEM\CurrentControlSet\Services\' + @AgentServiceName; 

INSERT  INTO #reg
        EXEC master..xp_regread 'HKEY_LOCAL_MACHINE', @key, 'ObjectName';
	
UPDATE  #reg
SET     keyname = @AgentServiceName
WHERE   keyname = 'ObjectName';


--Authentication Mode
INSERT  INTO #reg
        EXEC master..xp_loginconfig 'login mode';
--EXEC master..xp_regread N'HKEY_LOCAL_MACHINE',N'Software\Microsoft\MSSQLServer\MSSQLServer',N'LoginMode'


SELECT  @SQLSVRACC = value
FROM    #reg
WHERE   keyname = @SQLServiceName;
SELECT  @SQLAGTACC = value
FROM    #reg
WHERE   keyname = @AgentServiceName; 
SELECT  @LOGINMODE = CASE value
                        WHEN 'Windows NT Authentication' THEN 'Windows Authentication Mode'
                        WHEN 'Mixed' THEN 'Mixed Mode'
                        END
FROM    #reg
WHERE   keyname = 'login mode';


DROP TABLE #reg;


DECLARE @WindowsVersion VARCHAR(150);
DECLARE @Processorcount VARCHAR(150);
DECLARE @ProcessorType VARCHAR(150);
DECLARE @PhysicalMemory VARCHAR(150);

IF OBJECT_ID('tempdb..#Internal') IS NOT NULL
    EXEC ('Drop table #Internal');

CREATE TABLE #Internal
    (
        [Index] INT ,
        [Name] VARCHAR(20) ,
        [Internal_Value] VARCHAR(150) ,
        [Character_Value] VARCHAR(150)
    );

INSERT  INTO #Internal
        EXEC master..xp_msver;

SET @WindowsVersion = ( SELECT  [Character_Value]
                        FROM    #Internal
                        WHERE   [Name] = 'WindowsVersion'
                        );
SET @Processorcount = ( SELECT  [Character_Value]
                        FROM    #Internal
                        WHERE   [Name] = 'ProcessorCount'
                        );
SET @ProcessorType = ( SELECT   [Character_Value]
                        FROM     #Internal
                        WHERE    [Name] = 'ProcessorType'
                        );
SET @PhysicalMemory = ( SELECT  [Character_Value]
                        FROM    #Internal
                        WHERE   [Name] = 'PhysicalMemory'
                        );
SET @PhysicalMemory = ( SELECT  [Character_Value]
                        FROM    #Internal
                        WHERE   [Name] = 'PhysicalMemory'
                        );

DROP TABLE #Internal;

--Number Of Instaces On Server
DECLARE @GetInstances TABLE
	( Value nvarchar(100),
	InstanceNames nvarchar(100),
	Data nvarchar(100))

	Insert into @GetInstances
	EXECUTE xp_regread
		@rootkey = 'HKEY_LOCAL_MACHINE',
		@key = 'SOFTWARE\Microsoft\Microsoft SQL Server',
		@value_name = 'InstalledInstances'


SELECT  ISNULL(CONVERT(NVARCHAR(200), @@SERVERNAME),
                CONVERT(NVARCHAR(200), SERVERPROPERTY('MachineName'))) AS [ServerName] ,
        CONVERT(NVARCHAR(200), SERVERPROPERTY('ComputerNamePhysicalNetBIOS')) AS [MachineName] ,
        @@SERVICENAME AS [Instance] ,
        @Processorcount AS [ProcessorCount] ,
        @ProcessorNameString AS [ProcessorName] ,
        @PhysicalMemory AS [PhysicalMemory] ,
        @SQLSVRACC AS [SQLAccount] ,
        @SQLAGTACC AS [SQLAgentAccount] ,
        @LOGINMODE AS [AuthenticationnMode] ,
        @@VERSION AS [Version] ,
		CAST(SERVERPROPERTY('ProductVersion') as nvarchar(128)) [ProductVersion],
        CONVERT(NVARCHAR(200),SERVERPROPERTY('Edition')) AS [Edition] ,
        CONVERT(NVARCHAR(200),SERVERPROPERTY('Collation')) AS Collation ,
        CONVERT(NVARCHAR(200),SERVERPROPERTY('ProductLevel')) AS ProductLevel ,
        @SystemManufacturer + ' ' + @SystemModal AS [SystemModel] ,
        ( SELECT TOP 1
                    login_time AS ServiceStartTime
            FROM      sys.sysprocesses
            WHERE     spid = 1
        ) AS ServerStartTime,
		(SELECT	COUNT(1)
		FROM	@GetInstances GI) AS NumberOfInstaces,
		@OperatingSystem [OperatingSystem]
		OPTION (RECOMPILE);