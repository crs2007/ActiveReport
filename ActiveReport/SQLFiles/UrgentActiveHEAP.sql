-- =============================================
-- Author:		Sharon
-- Create date: 11/05/2016
-- Description:	Get Active tables without clustered index 
-- Prerequisite: None
-- Output:		#ActiveHEAP
-- =============================================
DECLARE @SQL VARCHAR(5000)   

IF OBJECT_ID('tempdb..##ResultsActiveHEAP') IS NOT NULL
	DROP TABLE ##ResultsActiveHEAP;
IF OBJECT_ID('tempdb..#ActiveHEAP') IS NULL 
	CREATE TABLE #ActiveHEAP([Database_Name] sysname,[schema_name] sysname ,[Table_Name] sysname);
CREATE TABLE ##ResultsActiveHEAP ([Database_Name] sysname, [schema_name] sysname , [Table_Name] sysname);
	
SET @SQL = N'USE [?] INSERT ##ResultsActiveHEAP
SELECT	DB_NAME() AS [Database_Name] ,
    SCHEMA_NAME(O.schema_id) AS [schema_name] ,
    O.name AS [Table_Name]
FROM    sys.indexes I
    INNER JOIN sys.objects O ON I.object_id = O.object_id
WHERE   O.is_ms_shipped = 0  /* Not shipped by Microsoft */
	AND I.index_id = 0 /* Index Id 0 = A Heap */
	AND O.type = ''U''
	AND DB_ID() > 4 
	AND DB_NAME() NOT IN (''NayaReportClientSide'',''ActiveReport'',''LiveMonitor'',''DBA'',''tempdb'','''','''')
OPTION(RECOMPILE);'

--Run the command against each database (IGNORE OFF-LINE DB)
BEGIN TRY
	EXEC sp_MSforeachdb @SQL;        
END TRY
BEGIN CATCH
      
END CATCH


INSERT	#ActiveHEAP([Database_Name] , [schema_name]  , [Table_Name] )
SELECT	[Database_Name] , [schema_name]  , [Table_Name]
FROM		##ResultsActiveHEAP 
OPTION(RECOMPILE);

SELECT	Database_Name ,
        schema_name ,
        Table_Name
FROM	#ActiveHEAP 
OPTION(RECOMPILE);

DROP TABLE ##ResultsActiveHEAP;
DROP TABLE #ActiveHEAP;