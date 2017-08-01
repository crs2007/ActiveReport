-- =============================================
-- Author:		Sharon
-- Create date: 19/05/2016
-- Update date: 27/10/2016 Software awareness
-- Description:	Get Database Info
-- Prerequisite:None
-- Output:		#DataBaseInfo
-- =============================================
DECLARE @MajorVersion INT;
IF OBJECT_ID('tempdb..#checkversion') IS NOT NULL DROP TABLE #checkversion;
CREATE TABLE #checkversion
    (
        version NVARCHAR(128) ,
        common_version AS SUBSTRING(version, 1,
                                    CHARINDEX('.', version) + 1) ,
        major AS PARSENAME(CONVERT(VARCHAR(32), version), 4) ,
        minor AS PARSENAME(CONVERT(VARCHAR(32), version), 3) ,
        build AS PARSENAME(CONVERT(VARCHAR(32), version), 2) ,
        revision AS PARSENAME(CONVERT(VARCHAR(32), version), 1)
    );
INSERT  INTO #checkversion ( version ) SELECT  CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(128));
SELECT  @MajorVersion = major + CASE WHEN minor = 0 THEN '00' ELSE minor end
FROM    #checkversion
OPTION  ( RECOMPILE );
IF OBJECT_ID('tempdb..#VLFInfo2008') IS NOT NULL DROP TABLE #VLFInfo2008;
CREATE TABLE #VLFInfo2008
    (
        FileID INT ,
        FileSize BIGINT ,
        StartOffset BIGINT ,
        FSeqNo BIGINT ,
        Status BIGINT ,
        Parity BIGINT ,
        CreateLSN NUMERIC(38)
    );
       
IF OBJECT_ID('tempdb..#VLFInfo') IS NOT NULL DROP TABLE #VLFInfo;
CREATE TABLE #VLFInfo
    (
        RecoveryUnitID INT ,
        FileID INT ,
        FileSize BIGINT ,
        StartOffset BIGINT ,
        FSeqNo BIGINT ,
        Status BIGINT ,
        Parity BIGINT ,
        CreateLSN NUMERIC(38)
    );

IF OBJECT_ID('tempdb..#VLFCountResults') IS NOT NULL DROP TABLE #VLFCountResults;
CREATE TABLE #VLFCountResults
    (
        DatabaseName sysname COLLATE DATABASE_DEFAULT ,
        VLFCount INT
    );

IF @MajorVersion > 1050
    BEGIN
        EXEC sp_MSforeachdb N'use [?]; 
INSERT INTO #VLFInfo 
EXEC sp_executesql N''DBCC LOGINFO([?]) WITH NO_INFOMSGS''; 
        
INSERT INTO #VLFCountResults 
SELECT DB_NAME(), COUNT(*) 
FROM #VLFInfo
OPTION(RECOMPILE); 
TRUNCATE TABLE #VLFInfo;';
    END;
ELSE
    BEGIN
           
        EXEC sp_MSforeachdb N'Use [?]; 
INSERT INTO #VLFInfo2008
EXEC sp_executesql N''DBCC LOGINFO([?]) WITH NO_INFOMSGS''; 
        
INSERT INTO #VLFCountResults 
SELECT DB_NAME(), COUNT(*) 
FROM #VLFInfo2008
OPTION(RECOMPILE); 
TRUNCATE TABLE #VLFInfo;';
    END;	
-------------------------------------------------------------------------------------------------------------------
DECLARE @DB_CRM TABLE
(DatabaseName sysname);
DECLARE @DB_SharePoint TABLE
(DatabaseName sysname);
DECLARE @DB_tfs TABLE
(DatabaseName sysname);
--CRM Dynamics
IF DB_ID('MSCRM_CONFIG') IS NOT NULL
BEGIN
	INSERT @DB_CRM
	SELECT D.name
	FROM   sys.databases D
	WHERE  D.name = 'MSCRM_CONFIG'
			OR D.name LIKE '%[_]MSCRM'
	UNION
	SELECT [DatabaseName] COLLATE DATABASE_DEFAULT
	FROM   [MSCRM_CONFIG].[dbo].[Organization]
	OPTION  ( RECOMPILE );
END
--SharePoint
INSERT @DB_SharePoint
EXEC sp_MSforeachdb 'SELECT TOP 1 ''?'' [DatabaseName]
FROM   [?].sys.database_principals DP
WHERE  DP.type = ''R'' AND DP.name IN (N''SPDataAccess'',N''SPReadOnly'')
OPTION  ( RECOMPILE );';
		
--Team Foundation Server Databases(TFS)
IF DB_ID('Tfs_Configuration') IS NOT NULL
BEGIN
	INSERT  @DB_tfs
	EXEC sp_MSforeachdb 'SELECT TOP 1 ''?''[DatabaseName]
FROM   [?].sys.database_principals DP
WHERE  DP.type = ''R'' AND DP.name = ''TfsWarehouseDataReader''
OPTION  ( RECOMPILE );'

	INSERT  @DB_tfs
	SELECT	CR.[DisplayName] COLLATE DATABASE_DEFAULT
	FROM	[Tfs_Configuration].[dbo].[tbl_CatalogResource] CR
			INNER JOIN [Tfs_Configuration].[dbo].[tbl_CatalogResourceType] RC ON RC.Identifier = CR.ResourceType
	WHERE	RC.DisplayName = 'Team Foundation Project Collection Database'
			AND CR.[DisplayName] COLLATE DATABASE_DEFAULT NOT IN(SELECT DatabaseName FROM @DB_tfs)
	UNION	SELECT name FROM sys.databases WHERE [name] IN ('TFS_Configuration','TFS_Warehouse','TFS_Analysis') AND state = 0 AND name NOT IN(SELECT DatabaseName FROM @DB_tfs)
	OPTION  ( RECOMPILE );
END
--------------------------------------------------------------------------------------------------------------------------------------
SELECT  D.name COLLATE DATABASE_DEFAULT [name],
        recovery_model_desc COLLATE DATABASE_DEFAULT [recovery_model_desc],
        is_read_only ,
        is_auto_close_on ,
        is_auto_shrink_on ,
        collation_name COLLATE DATABASE_DEFAULT [collation_name],
        state_desc COLLATE DATABASE_DEFAULT [state_desc],
        is_auto_create_stats_on ,
        is_auto_update_stats_on,
		VL.VLFCount,
		ISNULL(CONVERT(NVARCHAR(MAX),sp.name),CONVERT(NVARCHAR(MAX), master.dbo.fn_varbintohexstr(D.owner_sid))) [owner_sid],
		D.compatibility_level,
		D.page_verify_option_desc COLLATE DATABASE_DEFAULT [page_verify_option_desc],
        CASE WHEN D.name COLLATE DATABASE_DEFAULT IN ('BizTalkMsgBoxDB','BizTalkRuleEngineDb','SSODB','BizTalkHWSDb','BizTalkEDIDb','BAMArchive','BAMStarSchema','BAMPrimaryImport','BizTalkMgmtDb','BizTalkAnalysisDb','BizTalkTPMDb') THEN 1 ELSE 0 END [IsBizTalk],
        CASE WHEN D.name COLLATE DATABASE_DEFAULT IN (SELECT DatabaseName FROM @DB_CRM) THEN 1 ELSE 0 END [IsCRMDynamics],
        CASE WHEN D.name COLLATE DATABASE_DEFAULT IN (SELECT DatabaseName FROM @DB_SharePoint) THEN 1 ELSE 0 END [IsSharePoint],
        CASE WHEN D.name COLLATE DATABASE_DEFAULT IN (SELECT DatabaseName FROM @DB_tfs) THEN 1 ELSE 0 END [IsTFS]
FROM    sys.databases D
		LEFT JOIN #VLFCountResults VL ON VL.DatabaseName = D.name COLLATE DATABASE_DEFAULT
		LEFT JOIN sys.server_principals sp ON D.owner_sid = sp.sid
WHERE	D.database_id > 4
OPTION(RECOMPILE);
-- High VLF counts can affect write performance 
-- and they can make database restores and recovery take much longer
-- Try to keep your VLF counts under 200 in most cases	 
DROP TABLE #VLFInfo;
DROP TABLE #VLFCountResults;