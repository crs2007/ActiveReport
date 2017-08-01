-- =============================================
-- Author:		Sharon Rimer
-- Create date: 26/05/2016
-- Description:	Get CheckDB Info
-- Prerequisite: None
-- Output:		#DBCCRes
-- =============================================
IF OBJECT_ID('tempdb..#temp') IS NOT NULL DROP TABLE #temp;
IF OBJECT_ID('tempdb..#DBCCRes') IS NOT NULL DROP TABLE #DBCCRes;
CREATE TABLE #DBCCRes
(
    id INT IDENTITY(1, 1)
            PRIMARY KEY CLUSTERED ,
    DBName VARCHAR(500) ,
    dbccLastKnownGood DATETIME ,
    RowNum INT
);
CREATE TABLE #temp
(
    id INT IDENTITY(1, 1) ,
    ParentObject VARCHAR(255) ,
    [OBJECT] VARCHAR(255) ,
    Field VARCHAR(255) ,
    [VALUE] VARCHAR(255)
);
 
DECLARE @DBName sysname ,
		@SQLcmd VARCHAR(512);
 
DECLARE dbccpage CURSOR LOCAL FAST_FORWARD 
FOR
    SELECT  name
    FROM    sys.databases
    WHERE   state = 0
            AND database_id != 2;
OPEN dbccpage;
FETCH NEXT FROM dbccpage INTO @DBName;
WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @SQLcmd = 'Use [' + @DBName + '];' + CHAR(10) + CHAR(13);
        SET @SQLcmd = @SQLcmd + 'DBCC Page ( [' + @DBName
            + '],1,9,3) WITH TABLERESULTS,NO_INFOMSGS;' + CHAR(10) + CHAR(13);
 
        INSERT  INTO #temp
                EXECUTE ( @SQLcmd);
        SET @SQLcmd = '';
 
        INSERT  INTO #DBCCRes ( DBName , dbccLastKnownGood , RowNum)
        SELECT  @DBName ,
                VALUE ,
                ROW_NUMBER() OVER ( PARTITION BY Field ORDER BY VALUE ) AS Rownum
        FROM    #temp
        WHERE   Field = 'dbi_dbccLastKnownGood';
 
        TRUNCATE TABLE #temp;
 
        FETCH NEXT FROM dbccpage INTO @DBName;
    END;
CLOSE dbccpage;
DEALLOCATE dbccpage;
	
DROP TABLE #temp;
SELECT	*
FROM	#DBCCRes;
DROP TABLE #DBCCRes;