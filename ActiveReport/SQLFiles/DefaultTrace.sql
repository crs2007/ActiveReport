-- =============================================
-- Author:		Dror
-- Create date: 11/05/2016
-- Description:	Get Default Trace Scan for autogrowth
-- Prerequisite: dbo.configurations C
-- Output:		#DefaultTrace
-- =============================================
IF OBJECT_ID('tempdb..#DefaultTrace') IS NULL 
CREATE TABLE #DefaultTrace
(
    StartTime DATETIME ,
    EndTime DATETIME ,
    DatabaseName VARCHAR(255) ,
    EventClass INT
);

DECLARE @defualtracepath VARCHAR(255);
IF EXISTS(	SELECT TOP 1 1 FROM sys.configurations C WHERE name = 'default trace enabled' AND value = 1 ) AND EXISTS(SELECT TOP 1 1 FROM sys.traces )
BEGIN
    SELECT  @defualtracepath = [path]
    FROM    sys.traces
    WHERE   id = 1;
    INSERT  INTO #DefaultTrace
            SELECT  StartTime ,
                    EndTime ,
                    DatabaseName ,
                    EventClass
            FROM    ::
                    fn_trace_gettable(@defualtracepath, DEFAULT) AS ftg
            WHERE   EventClass IN ( 18, 92, 93, 94, 95 )
                    AND StartTime > DATEADD(WW, -1,GETDATE())
            UNION ALL
            SELECT  MIN(StartTime) AS StartTime ,
                    GETDATE() AS EndTime ,
                    NULL AS DatabaseName ,
                    1 AS EventClass
            FROM    ::
                    fn_trace_gettable(@defualtracepath, DEFAULT) AS ftg
            WHERE   StartTime > DATEADD(WW, -1, GETDATE());
END;

SELECT	StartTime,
		EndTime,
		DatabaseName,
		EventClass
FROM	#DefaultTrace
OPTION(RECOMPILE);