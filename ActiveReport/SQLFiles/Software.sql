-- =============================================
-- Author:      Sharon
-- Create date: 09/08/2016
-- Description: 
-- =============================================
IF OBJECT_ID('tempdb..#Software') IS NULL
CREATE TABLE #Software
    (
        [Software] NVARCHAR(MAX),
		[Status] BIT
    );
DECLARE @RC INT;
SET @RC = 0;
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
	SET @RC = @@ROWCOUNT;
END
DECLARE @IsCRMDynamicsON BIT;
SET @IsCRMDynamicsON = 0;
DECLARE @IsBizTalkON BIT;
SET @IsBizTalkON = 0;
DECLARE @IsSharePointON BIT;
SET @IsSharePointON = 0;
DECLARE @IsTFSON BIT;
SET @IsTFSON = 0;
IF @RC > 0 OR EXISTS (SELECT TOP 1 1 FROM sys.server_principals SP WHERE  SP.name = 'MSCRMSqlLogin')
	SET @IsCRMDynamicsON = 1 ;
		
--BizTalk
SELECT @IsBizTalkON = 1 
WHERE EXISTS (
SELECT TOP 1 1
FROM   sys.databases D
WHERE  D.name IN (N'BizTalkMsgBoxDB',N'BizTalkRuleEngineDb',N'SSODB',N'BizTalkHWSDb',N'BizTalkEDIDb',N'BAMArchive',N'BAMStarSchema',N'BAMPrimaryImport',N'BizTalkMgmtDb',N'BizTalkAnalysisDb',N'BizTalkTPMDb')
) OPTION  ( RECOMPILE );

--SharePoint
INSERT @DB_SharePoint
EXEC sp_MSforeachdb 'SELECT TOP 1 ''?'' [DatabaseName]
FROM   [?].sys.database_principals DP
WHERE  DP.type = ''R'' AND DP.name IN (N''SPDataAccess'',N''SPReadOnly'')
OPTION  ( RECOMPILE );'
SELECT @IsSharePointON = 1 
WHERE EXISTS (SELECT TOP 1 1 FROM @DB_SharePoint);
		
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
SELECT @IsTFSON = 1 WHERE EXISTS (SELECT TOP 1 1 FROM @DB_tfs);

SELECT 'SharePoint' [Software] ,@IsSharePointON [Status]
UNION ALL SELECT 'BizTalk' [Software] ,@IsBizTalkON [Status]
UNION ALL SELECT 'CRMDynamics' [Software] ,@IsCRMDynamicsON [Status]
UNION ALL SELECT 'TFS' [Software] ,@IsTFSON [Status];