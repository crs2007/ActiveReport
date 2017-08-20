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

CREATE TABLE ##ResultsActiveHEAP ([Database_Name] sysname, [schema_name] sysname , [Table_Name] sysname,[RowCounts] BIGINT,TotalSpaceGB NUMERIC(36,2),UsedSpaceGB NUMERIC(36,2),UnusedSpaceGB NUMERIC(36,2));
	
SET @SQL = N'USE [?] INSERT ##ResultsActiveHEAP
SELECT	DB_NAME() AS [Database_Name] ,
		s.name AS [schema_name] ,
		O.name AS [Table_Name],
		p.rows AS RowCounts,
		CAST(ROUND(((SUM(a.total_pages) * 8) / 1024.00/ 1024.00), 2) AS NUMERIC(36, 2)) AS TotalSpaceGB,
		CAST(ROUND(((SUM(a.used_pages) * 8) / 1024.00/ 1024.00), 2) AS NUMERIC(36, 2)) AS UsedSpaceGB, 
		CAST(ROUND(((SUM(a.total_pages) - SUM(a.used_pages)) * 8) / 1024.00/ 1024.00, 2) AS NUMERIC(36, 2)) AS UnusedSpaceGB
FROM    sys.indexes I
		INNER JOIN sys.objects O ON I.object_id = O.object_id
		LEFT OUTER JOIN sys.schemas s ON O.schema_id = s.schema_id
		INNER JOIN sys.partitions p ON I.object_id = p.OBJECT_ID AND I.index_id = p.index_id
		INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
WHERE   O.is_ms_shipped = 0  /* Not shipped by Microsoft */
		AND I.index_id = 0 /* Index Id 0 = A Heap */
		AND O.type = ''U''
		AND DB_ID() > 4 
		AND DB_NAME() NOT IN (''NayaReportClientSide'',''ActiveReport'',''LiveMonitor'',''DBA'',''tempdb'','''','''')
GROUP BY s.name, O.name,p.rows
HAVING CAST(ROUND(((SUM(a.total_pages) * 8) / 1024.00/ 1024.00), 2) AS NUMERIC(36, 2)) > 0
OPTION(RECOMPILE);'
--Run the command against each database (IGNORE OFF-LINE DB)
BEGIN TRY
	EXEC sp_MSforeachdb @SQL;        
END TRY
BEGIN CATCH
      
END CATCH


SELECT	[Database_Name],
		[schema_name],
		Table_Name,
		RowCounts,
		TotalSpaceGB,
		UsedSpaceGB,
		UnusedSpaceGB
FROM	##ResultsActiveHEAP 
ORDER BY RowCounts DESC
OPTION(RECOMPILE);

DROP TABLE ##ResultsActiveHEAP;