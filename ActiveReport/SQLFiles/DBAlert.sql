-- =============================================
-- Author:		Sharon Rimer
-- Create date: 06/06/2017
-- Description:	Get Arithmetic overflow
-- Prerequisite: None
-- Output:		#DBAlert
-- =============================================
DECLARE @DatabaseName sysname;
	DECLARE @cmd NVARCHAR(max);
	IF OBJECT_ID('tempdb..#DBAlert') IS NULL
	CREATE TABLE #DBAlert([Type] sysname NULL,
		[DatabaseName] sysname NULL,
		[Note] NVARCHAR(MAX) NULL
		);


	DECLARE cuMaxIdent CURSOR LOCAL FAST_FORWARD READ_ONLY FOR 
	SELECT  name
	FROM    sys.databases
	WHERE   state = 0
			AND database_id > 4
	OPTION(RECOMPILE);
	OPEN cuMaxIdent;
	FETCH NEXT FROM cuMaxIdent INTO @DatabaseName;
	WHILE @@FETCH_STATUS = 0
	BEGIN 
		SET @cmd = N'USE ' + QUOTENAME(@DatabaseName) + ';

		
DECLARE @DataTypeMaxValue TABLE(DataType sysname, MaxValue bigint);
 
INSERT @DataTypeMaxValue
SELECT ''tinyint'' , 255
UNION ALL SELECT ''smallint'' , 32767
UNION ALL SELECT ''int'' , 2147483647
UNION ALL SELECT ''bigint'' , 9223372036854775807;
 
INSERT	#DBAlert
SELECT	''MaxIdentity'' [Type],DB_NAME() DB,s.name + ''.'' + t.name + ''.'' + sc.name + ''('' + dt.DataType + '') have reached up to '' + CONVERT(VARCHAR(50),M.ReachMaxValuePercent)  + ''%'' 
FROM	sys.identity_columns sc
		INNER JOIN sys.tables t ON t.object_id = sc.object_id 
		INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
		INNER JOIN sys.types ty ON sc.system_type_id = ty.system_type_id
		INNER JOIN @DataTypeMaxValue dt ON ty.name = dt.DataType COLLATE DATABASE_DEFAULT
		CROSS APPLY(SELECT TOP 1 (convert(decimal(9,7),CONVERT(bigint,sc.last_value)*1.00/dt.MaxValue))*100 [ReachMaxValuePercent])M
WHERE	t.is_ms_shipped = 0
		AND sc.last_value IS NOT NULL
		AND M.ReachMaxValuePercent > 80
ORDER BY sc.last_value DESC
OPTION(RECOMPILE);';

	   EXECUTE sp_executesql @cmd;

	IF ( SELECT SERVERPROPERTY('ProductMajorVersion')) >= '11'
	BEGIN
	   SET @cmd = N'USE ' + QUOTENAME(@DatabaseName) + ';
INSERT	#DBAlert
SELECT		[type],
			DB_NAME() DB,
			''Sequences '' + S.name + '' have reached up to '' + CONVERT(VARCHAR(50), M.ReachMaxValuePercent) + ''% of '' + Tp.name
FROM		sys.sequences S
			INNER JOIN sys.types Tp ON S.system_type_id = Tp.system_type_id
										AND S.user_type_id = Tp.user_type_id
			CROSS APPLY (VALUES((CAST(current_value AS FLOAT) / CAST(maximum_value AS FLOAT))*100))M(ReachMaxValuePercent)
WHERE		M.ReachMaxValuePercent > 80
ORDER BY	M.ReachMaxValuePercent DESC
OPTION(RECOMPILE);';
		EXECUTE sp_executesql @cmd;
	END

       
            FETCH NEXT FROM cuMaxIdent INTO @DatabaseName;
        END;
    CLOSE cuMaxIdent;
    DEALLOCATE cuMaxIdent;

SELECT	DA.Type ,
        DA.DatabaseName ,
        DA.Note
FROM	#DBAlert DA
OPTION(RECOMPILE);

DROP TABLE #DBAlert;