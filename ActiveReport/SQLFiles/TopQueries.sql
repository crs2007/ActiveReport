-- =============================================
-- Author:		Sharon
-- Create date: 2013
-- Update date: 
-- Description:	TopTenQueries
-- =============================================
SELECT TOP 10
        'I/O' AS [CheckType] ,
        qs.execution_count , 
        ( qs.total_physical_reads + qs.total_logical_writes )
        / qs.execution_count AS [AvgPhysicalIO] , 
        qs.last_execution_time ,
        ( qs.total_elapsed_time / 1000000 ) / qs.execution_count AS [AvgDuration] ,
        CASE WHEN SUBSTRING(qt.[text], CASE WHEN Offset = 0 THEN 0 ELSE Offset +1 END,
                    ( CASE WHEN qs.statement_end_offset = -1
                            THEN LEN(CONVERT(NVARCHAR(MAX), qt.[text]))
                                * 2
                            ELSE qs.statement_end_offset
                    END - qs.statement_start_offset ) / 2)  = '' THEN qt.[text] 
		ELSE 
		SUBSTRING(qt.[text], qs.statement_start_offset / 2,
                    ( CASE WHEN qs.statement_end_offset = -1
                            THEN LEN(CONVERT(NVARCHAR(MAX), qt.[text]))
                                * 2
                            ELSE qs.statement_end_offset
                    END - qs.statement_start_offset ) / 2)
		END AS query_text ,
        DB_NAME(qt.[dbid]) AS database_name
FROM    sys.dm_exec_query_stats qs
        CROSS APPLY sys.dm_exec_sql_text(qs.[sql_handle]) AS qt
		CROSS APPLY(SELECT TOP 1 qs.statement_start_offset / 2 [Offset])O
UNION ALL 
SELECT TOP 10
        'Mem' AS [CheckType] ,
        qs.execution_count ,
        qs.total_logical_reads / qs.execution_count AS [AvgLogicalIO] , 
        qs.last_execution_time ,
        ( qs.total_elapsed_time / 1000000 ) / qs.execution_count AS [AvgDuration] ,
        CASE WHEN SUBSTRING(qt.[text], CASE WHEN Offset = 0 THEN 0 ELSE Offset +1 END,
                    ( CASE WHEN qs.statement_end_offset = -1
                            THEN LEN(CONVERT(NVARCHAR(MAX), qt.[text]))
                                * 2
                            ELSE qs.statement_end_offset
                    END - qs.statement_start_offset ) / 2)  = '' THEN qt.[text] 
		ELSE 
		SUBSTRING(qt.[text], qs.statement_start_offset / 2,
                    ( CASE WHEN qs.statement_end_offset = -1
                            THEN LEN(CONVERT(NVARCHAR(MAX), qt.[text]))
                                * 2
                            ELSE qs.statement_end_offset
                    END - qs.statement_start_offset ) / 2)
		END AS query_text ,
        DB_NAME(qt.[dbid]) AS database_name
FROM    sys.dm_exec_query_stats qs
        CROSS APPLY sys.dm_exec_sql_text(qs.[sql_handle]) AS qt
		CROSS APPLY(SELECT TOP 1 qs.statement_start_offset / 2 [Offset])O
UNION ALL
SELECT TOP 10
        'CPU' AS [CheckType] ,
        qs.execution_count , 
        qs.total_worker_time / qs.execution_count AS [AvgCPUTime] ,
        qs.last_execution_time ,
        ( qs.total_elapsed_time / 1000000 ) / qs.execution_count AS [AvgDuration] ,
        CASE WHEN SUBSTRING(qt.[text], CASE WHEN Offset = 0 THEN 0 ELSE Offset +1 END,
                    ( CASE WHEN qs.statement_end_offset = -1
                            THEN LEN(CONVERT(NVARCHAR(MAX), qt.[text]))
                                * 2
                            ELSE qs.statement_end_offset
                    END - qs.statement_start_offset ) / 2)  = '' THEN qt.[text] 
		ELSE 
		SUBSTRING(qt.[text], CASE WHEN Offset = 0 THEN 0 ELSE Offset +1 END,
                    ( CASE WHEN qs.statement_end_offset = -1
                            THEN LEN(CONVERT(NVARCHAR(MAX), qt.[text]))
                                * 2
                            ELSE qs.statement_end_offset
                    END - qs.statement_start_offset ) / 2)
		END AS query_text ,
        DB_NAME(qt.[dbid]) AS database_name
FROM    sys.dm_exec_query_stats qs
        CROSS APPLY sys.dm_exec_sql_text(qs.[sql_handle]) AS qt
		CROSS APPLY(SELECT TOP 1 qs.statement_start_offset / 2 [Offset])O
ORDER BY 3 DESC
OPTION (RECOMPILE); 