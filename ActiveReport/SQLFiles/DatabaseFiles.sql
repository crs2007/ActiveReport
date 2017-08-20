-- =============================================
-- Author:		Sharon Rimer
-- Create date: 11/05/2016
-- Description:	Get Databse Files Information 
-- Prerequisite: None
-- =============================================
DECLARE @SQL VARCHAR(5000);  

IF OBJECT_ID('tempdb..##DBFResults') IS NOT NULL
	DROP TABLE ##DBFResults;
CREATE TABLE ##DBFResults ([Database Name] varchar(200) NULL, [File Name] varchar(1000) NULL,[Available Space in Mb] INT NULL,FG_Name sysname NULL,FG_Type sysname NULL,FG_Default int NULL);

SELECT @SQL =    
'USE [?] INSERT INTO ##DBFResults([Database Name], [File Name],[Available Space in Mb],FG_Name, FG_Type, FG_Default )    
SELECT	''?'',   
		fil.[name] AS [File Name],
		CASE ceiling(fil.[size]/128)   
		WHEN 0 THEN (1 - CAST(FILEPROPERTY(fil.[name], ''SpaceUsed''' + ') as int) /128)   
		ELSE (([size]/128) - CAST(FILEPROPERTY(fil.[name], ''SpaceUsed''' + ') as int) /128)   
		END AS [Available Space in Mb],
		fg.name,fg.type_desc,fg.is_default
FROM	sys.database_files fil
		LEFT JOIN sys.data_spaces fg ON fil.data_space_id = fg.data_space_id
OPTION(RECOMPILE);'   

--Run the command against each database (IGNORE OFF-LINE DB)
EXEC sp_MSforeachdb @SQL;

SELECT	D.name [Database_Name],mf.name [File_Name],MF.physical_name [Physical_Name],
		CASE MF.type_desc 
		WHEN 'ROWS' THEN 'Data'
		WHEN 'LOG' THEN 'Log'
		WHEN 'FILESTREAM' THEN 'FileStream'
		WHEN 'FULLTEXT' THEN 'FullText'
		ELSE 'Unknowen' END  [File_Type],
		MF.size * 8 / 1024 [Total_Size],--MB
		R.[Available Space in Mb] [Free_Space],--MB
		CONVERT(VARCHAR(25),CASE WHEN MF.is_percent_growth = 1 THEN MF.growth ELSE MF.growth * 8 / 1024 END) + CASE WHEN MF.is_percent_growth = 1 THEN '%' ELSE 'MB' END [Growth_Units],
		CASE WHEN MF.max_size = -1 OR MF.max_size = 268435456 THEN NULL ELSE CONVERT(VARCHAR(25),MF.max_size) END [Max_Size],--[Max File Size in MB]
		MF.data_space_id [FG_id],
		R.FG_Name,R.FG_Type,R.FG_Default
FROM	sys.databases D
		INNER JOIN sys.master_files MF ON MF.database_id = D.database_id
		LEFT JOIN ##DBFResults R ON D.name = R.[Database Name] AND R.[File Name] = MF.name
OPTION(RECOMPILE);

DROP TABLE ##DBFResults;