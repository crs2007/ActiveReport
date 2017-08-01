-- =============================================
-- Author:		Dror
-- Create date: 11/05/2016
-- Update date: 11/06/2016 Sharon Fillter + add Agg + Memory Issue
-- Update date: 03/07/2016 Sharon Agg
-- Description:	Get SQL Error Log 
-- Prerequisite: clr
-- Output:		#errorsLog + @MemoryIssue
-- =============================================
DECLARE @NumErrorLogs INT;
IF (NOT IS_SRVROLEMEMBER(N'securityadmin') = 1) 
BEGIN 
    RAISERROR(15003,-1,-1, N'securityadmin');
END
DECLARE @StartDate DATETIME;
DECLARE @EndDate DATETIME;
SELECT	@StartDate = DATEADD(WW, -1, GETDATE()),
		@EndDate = GETDATE(); 
SET @NumErrorLogs = 6;
IF OBJECT_ID('tempdb..#errorMemory_stage') IS NOT NULL
            DROP TABLE #errorMemory_stage
CREATE TABLE #errorMemory_stage
    (
        LogDate DATETIME ,
        ProcessesInfo VARCHAR(100) ,
        [Text] VARCHAR(MAX)
    );
DECLARE @cnt INT;
DECLARE @errorlog INT;
DECLARE @errorcount INT;
SET @cnt = 0;
SET @errorlog = 0;
SET @errorcount = 0;
    IF OBJECT_ID('tempdb..#errorlog_stage') IS NOT NULL
            DROP TABLE #errorlog_stage
CREATE TABLE #errorlog_stage
    (
        LogDate DATETIME ,
        ProcessesInfo VARCHAR(100) ,
        [Text] VARCHAR(MAX)
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
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Error: 701', NULL, @StartDate, @EndDate;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Error: 17300', NULL, @StartDate, @EndDate;;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Error: 17312', NULL, @StartDate, @EndDate;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'There is insufficient system memory in resource pool', NULL, @StartDate, @EndDate;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'The error is printed in terse mode because there was error during formatting. Tracing, ETW, notifications etc are skipped.', NULL, @StartDate, @EndDate;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'A significant part of sql server process memory has been paged out. This may result in a performance degradation.', NULL, @StartDate, @EndDate;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Failed allocate pages', NULL, @StartDate, @EndDate;

INSERT  #errorlog_stage
EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'OBJECTSTORE_LOCK_MANAGER', NULL, @StartDate, @EndDate;
SET @errorlog = @errorlog + 1;

	
INSERT #errorMemory_stage
SELECT	LogDate ,ProcessesInfo,[Text]
FROM	#errorlog_stage
WHERE	LogDate > DATEADD(WW, -1, GETDATE())
		AND ([Text] LIKE 'Error: 701%'
		OR [Text] LIKE 'Error: 17300%'
		OR [Text] LIKE 'Error: 17312%'
		OR [Text] LIKE '%There is insufficient system memory in resource pool%'
		OR [Text] LIKE '%The error is printed in terse mode because there was error during formatting. Tracing, ETW, notifications etc are skipped.%'
		OR [Text] LIKE 'A significant part of sql server process memory has been paged out. This may result in a performance degradation.%'
		OR [Text] LIKE 'Failed allocate pages%'
		OR [Text] LIKE '%OBJECTSTORE_LOCK_MANAGER%')
OPTION(RECOMPILE);

WHILE (@errorcount < 1 AND @cnt < @NumErrorLogs)
BEGIN
    BEGIN TRY
		TRUNCATE TABLE #errorlog_stage;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Error: 701', NULL, @StartDate, @EndDate;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Error: 17300', NULL, @StartDate, @EndDate;;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Error: 17312', NULL, @StartDate, @EndDate;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'There is insufficient system memory in resource pool', NULL, @StartDate, @EndDate;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'The error is printed in terse mode because there was error during formatting. Tracing, ETW, notifications etc are skipped.', NULL, @StartDate, @EndDate;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'A significant part of sql server process memory has been paged out. This may result in a performance degradation.', NULL, @StartDate, @EndDate;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'Failed allocate pages', NULL, @StartDate, @EndDate;

		INSERT  #errorlog_stage
		EXEC master.dbo.xp_readerrorlog @errorlog, 1,N'OBJECTSTORE_LOCK_MANAGER', NULL, @StartDate, @EndDate;
		SET @cnt = @cnt + 1;

 		INSERT #errorMemory_stage
		SELECT	LogDate ,ProcessesInfo,[Text]
		FROM	#errorlog_stage
		WHERE	LogDate > DATEADD(WW, -1, GETDATE())
				AND ([Text] LIKE 'Error: 701%'
				OR [Text] LIKE 'Error: 17300%'
				OR [Text] LIKE 'Error: 17312%'
				OR [Text] LIKE '%There is insufficient system memory in resource pool%'
				OR [Text] LIKE '%The error is printed in terse mode because there was error during formatting. Tracing, ETW, notifications etc are skipped.%'
				OR [Text] LIKE 'A significant part of sql server process memory has been paged out. This may result in a performance degradation.%'
				OR [Text] LIKE 'Failed allocate pages%'
				OR [Text] LIKE '%OBJECTSTORE_LOCK_MANAGER%')
		OPTION(RECOMPILE);
                     
    END TRY
    BEGIN CATCH
        SET @errorcount = @errorcount + 1;
    END CATCH;
END;
		
SELECT 'There is over ' + CONVERT(VARCHAR(5),MAX(T.Cnt))+ ' cases of memory pressure alerts in SQL Server Error Log.' [MemoryIssue]
FROM (
	SELECT	LEFT(EMS.Text,40)[Text],COUNT_BIG(1)[Cnt]--,MIN(EMS.Text),LEN(MIN(EMS.Text))
	FROM	#errorMemory_stage EMS
	GROUP BY LEFT(EMS.Text,40))T
	OPTION(RECOMPILE);

DROP TABLE #errorlog_stage;
DROP TABLE #errorMemory_stage;