-- =============================================
-- Author:		Sharon Rimer
-- Create date: 22/06/2016
-- Update date: 
-- Description:	Get Deadlock Count
-- =============================================
SELECT  cntr_value AS NumOfDeadLocks
FROM    sys.dm_os_performance_counters
WHERE   object_name = 'SQLServer:Locks'
		AND counter_name = 'Number of Deadlocks/sec'
		AND instance_name = '_Total'
OPTION (RECOMPILE); 

