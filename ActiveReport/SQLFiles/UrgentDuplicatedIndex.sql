-- =============================================
-- Author:		Dror
-- Create date: 26/05/2016
-- Update date: 30/06/2016 Sharon TRY + Catch
--				26/12/2016 Sharon only nun = nun
-- Description:	Get Duplicated Index Information
-- Prerequisite:None
-- Output:		#missing_Indexes
-- =============================================
IF OBJECT_ID('tempdb..#duplicatedindexes') IS NULL 
		CREATE TABLE #duplicatedindexes
        (
            [DBName] VARCHAR(100) ,
            [table] VARCHAR(250) ,
            [index] VARCHAR(250) ,
            [exactduplicate] VARCHAR(250),
			[PrimaryIndexSeekCount] BIGINT,
			[SecondaryIndexSeekCount] BIGINT,
			[PrimaryIndexScanCount] BIGINT,
			[SecondaryIndexScanCount] BIGINT
        );

DECLARE @DaBName VARCHAR(100);
DECLARE @sqlcmd1 VARCHAR(MAX);
DECLARE dbccpage CURSOR LOCAL STATIC FORWARD_ONLY READ_ONLY
FOR
    SELECT  name
    FROM    sys.databases
    WHERE   state = 0
			AND HAS_DBACCESS(name) = 0
			AND database_id > 4
	OPTION(RECOMPILE);
--And name NOT IN ('tempdb')
;
 
OPEN dbccpage;
FETCH NEXT FROM dbccpage INTO @DaBName;
WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sqlcmd1 = 'Use [' + @DaBName + '];' + CHAR(10)
            + CHAR(13);
        SET @sqlcmd1 = @sqlcmd1 + '
WITH indexcols AS
(	SELECT	i.object_id as id, i.index_id as indid, name,
			(SELECT case keyno when 0 then NULL else colid end as [data()]
			FROM	sys.sysindexkeys as k
			WHERE	k.id = i.object_id
					AND k.indid = i.index_id
			ORDER BY keyno, colid
			FOR XML PATH('''')) as cols,
			(SELECT CASE keyno when 0 then colid else NULL end as [data()]
			FROM	sys.sysindexkeys as k
			WHERE	k.id = i.object_id
					AND k.indid = i.index_id
			ORDER by colid
			FOR XML PATH('''')) as inc,
			i.type_desc,
			i.is_unique,
			ISNULL(u.user_seeks,0) [user_seeks],
			ISNULL(u.user_scans,0) [user_scans]
	FROM	sys.indexes as i
			LEFT JOIN [sys].[dm_db_index_usage_stats] u ON (i.OBJECT_ID = u.OBJECT_ID)
				AND i.[index_id] = u.[index_id]
				AND u.[database_id] = DB_ID()
	WHERE	i.is_disabled = 0
)
INSERT INTO #duplicatedindexes
SELECT	DB_Name(),
		object_schema_name(c1.id) + ''.'' + object_name(c1.id) as [table],
		c1.name as [index],
		c2.name as [exactduplicate],
		c1.user_seeks [PrimaryIndexSeekCount],
		c2.user_seeks [SecondaryIndexSeekCount],
		c1.user_scans [PrimaryIndexScanCount],
		c2.user_scans [SecondaryIndexScanCount]
FROM	indexcols as c1
		INNER JOIN indexcols as c2 ON c1.id = c2.id
		AND c1.indid < c2.indid
		AND c1.cols = c2.cols
		AND c1.inc = c2.inc
		AND c1.type_desc = c2.type_desc
		AND c1.is_unique = c2.is_unique
OPTION (RECOMPILE);' + CHAR(10) + CHAR(13);
 
        BEGIN TRY
			INSERT INTO #duplicatedindexes EXECUTE ( @sqlcmd1 );
		END TRY
		BEGIN CATCH
		END CATCH

        SET @sqlcmd1 = '';
  
        FETCH NEXT FROM dbccpage INTO @DaBName;
    END;
CLOSE dbccpage;
DEALLOCATE dbccpage;

SELECT	D.DBName ,
        D.[table] ,
        D.[index] ,
        D.exactduplicate ,
        D.PrimaryIndexSeekCount ,
        D.SecondaryIndexSeekCount ,
        D.PrimaryIndexScanCount ,
        D.SecondaryIndexScanCount
FROM	#duplicatedindexes D
OPTION (RECOMPILE);