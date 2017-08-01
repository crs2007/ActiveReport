-- =============================================
-- Author:		Sharon Rimer
-- Create date: 11/05/2016
-- Update date: 25/06/2017 Sharon new script
-- Description:	Get Missing index Information
-- Prerequisite: None
-- Output:		#missing_Indexes
-- =============================================
IF OBJECT_ID('tempdb..#MissingIndexInfo') IS NOT NULL
    DROP TABLE #MissingIndexInfo;
       
WITH XMLNAMESPACES (DEFAULT 'http://schemas.microsoft.com/sqlserver/2004/07/showplan')
SELECT query_plan,
        n.value('(@StatementText)[1]', 'VARCHAR(4000)')                                                                                          AS [Code],
        n.value('(//MissingIndexGroup/@Impact)[1]', 'FLOAT')                                                                              AS Impact,
        DB_ID(REPLACE(REPLACE(n.value('(//MissingIndex/@Database)[1]', 'VARCHAR(128)'), '[', ''), ']', '')) AS database_id,
        OBJECT_ID(
                n.value('(//MissingIndex/@Database)[1]', 'VARCHAR(128)') + '.'
                + n.value('(//MissingIndex/@Schema)[1]', 'VARCHAR(128)') + '.'
                + n.value('(//MissingIndex/@Table)[1]', 'VARCHAR(128)'))                                                                   AS [Object_ID],
        n.value('(//MissingIndex/@Database)[1]', 'VARCHAR(128)')                                                                          AS [Database],
        n.value('(//MissingIndex/@Schema)[1]', 'VARCHAR(128)')                                                                            AS [Schema],
        n.value('(//MissingIndex/@Table)[1]', 'VARCHAR(128)')                                                                             AS [Table],
        (      SELECT DISTINCT
                            c.value('(@Name)[1]', 'VARCHAR(128)') + ', '
                FROM   n.nodes('//ColumnGroup') AS t(cg)
                            CROSS APPLY cg.nodes('Column') AS r(c)
                WHERE  cg.value('(@Usage)[1]', 'VARCHAR(128)') = 'EQUALITY'
                FOR XML PATH(''))                                                                                                                                     AS equality_columns,
        (      SELECT DISTINCT
                            c.value('(@Name)[1]', 'VARCHAR(128)') + ', '
                FROM   n.nodes('//ColumnGroup') AS t(cg)
                            CROSS APPLY cg.nodes('Column') AS r(c)
                WHERE  cg.value('(@Usage)[1]', 'VARCHAR(128)') = 'INEQUALITY'
                FOR XML PATH(''))                                                                                                                                     AS inequality_columns,
        (      SELECT DISTINCT
                            c.value('(@Name)[1]', 'VARCHAR(128)') + ', '
                FROM   n.nodes('//ColumnGroup') AS t(cg)
                            CROSS APPLY cg.nodes('Column') AS r(c)
                WHERE  cg.value('(@Usage)[1]', 'VARCHAR(128)') = 'INCLUDE'
                FOR XML PATH(''))                                                                                                                                     AS include_columns,
                execution_count,total_logical_reads,total_worker_time,last_execution_time,
                execution_count  * 0.1 *
                total_logical_reads * 
                total_worker_time * 
                CONVERT(INT,n.value('(//MissingIndexGroup/@Impact)[1]', 'FLOAT')) [Factor]
INTO   #MissingIndexInfo
FROM
        (      SELECT       query_plan,execution_count,total_logical_reads,total_worker_time,last_execution_time
                FROM
                            (      SELECT DISTINCT
                                                plan_handle,execution_count,total_logical_reads,total_worker_time,last_execution_time
                                    FROM   sys.dm_exec_query_stats WITH (NOLOCK)) AS qs
                            OUTER APPLY sys.dm_exec_query_plan(qs.plan_handle)tp
                WHERE  tp.query_plan.exist('//MissingIndex') = 1) AS tab(query_plan,execution_count,total_logical_reads,total_worker_time,last_execution_time)
        CROSS APPLY query_plan.nodes('//StmtSimple') AS q(n)
WHERE  n.exist('QueryPlan/MissingIndexes') = 1;

-- Trim trailing comma from lists
UPDATE  #MissingIndexInfo
SET     equality_columns = LEFT(equality_columns,
                                LEN(equality_columns) - 1) ,
        inequality_columns = LEFT(inequality_columns,
                                    LEN(inequality_columns) - 1) ,
        include_columns = LEFT(include_columns,
                                LEN(include_columns) - 1);

SELECT TOP ( 20 )
        [Schema] + '.' + [Table] [Object_name] ,
        Factor improvement_measure ,
        equality_columns,
        inequality_columns,
        include_columns,
        'CREATE NONCLUSTERED INDEX IX_' + REPLACE(REPLACE([Table], '[',
                                                        ''), ']', '')
        + '_' + REPLACE(REPLACE(REPLACE(equality_columns, '[', ''),
                                ']', ''), ', ', '') + '
ON ' + [Database] + '.' + [Schema] + '.' + [Table] + ' ('
        + CASE WHEN equality_columns IS NULL THEN inequality_columns
                ELSE equality_columns
            END + ')'
        + CASE WHEN include_columns IS NOT NULL
                THEN ' INCLUDE (' + include_columns + ')'
                ELSE ''
            END
        + '
WITH (PAD_INDEX = ON, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = ON, DROP_EXISTING = OFF, ONLINE = ON, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON);' create_index_statement ,
        [Database] DatabaseName ,
        MAX(Code) [Code] ,
        SUM(execution_count) [ExecutionCount] ,
        SUM(total_logical_reads) Total_Logical_Reads ,
        SUM(total_worker_time) Total_CPU_time ,
        MAX(last_execution_time) [ExecutionTime]
FROM    #MissingIndexInfo
GROUP BY [Database] ,
        [Schema] ,
        [Table] ,
        equality_columns ,
        inequality_columns ,
        include_columns ,
        Factor
ORDER BY Factor DESC
OPTION (RECOMPILE);

DROP TABLE #MissingIndexInfo;