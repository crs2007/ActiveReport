-- =============================================
-- Author:		Sharon
-- Create date: 11/05/2016
-- Description:	Get Unused Job Info
-- Prerequisite: None
-- Output:		#UnusedJobStatus
-- =============================================
SELECT	J.name [JobName], 
		CASE WHEN J.[enabled] = 1 THEN 'Enabled' ELSE 'Disabled ***' END[Status], 
		JA.LastRun,
		NULL TimePassed,
		JA.NextRun, JV.[description], JC.name JobCategory
FROM	msdb.dbo.sysjobs J
		INNER JOIN msdb.dbo.sysjobs_view JV      ON J.job_id = JV.job_id
		INNER JOIN
				(
				SELECT job_id, MAX(last_executed_step_date) LastRun, MAX(next_scheduled_run_date) NextRun
				FROM   msdb.dbo.sysjobactivity 
				GROUP  BY job_id
				) JA ON J.job_id = JA.job_id
		INNER JOIN msdb.dbo.syscategories JC ON J.category_id = JC.category_id
		LEFT JOIN msdb.dbo.sysalerts A ON J.job_id = A.job_id
WHERE	A.id is null
		and JV.[description] NOT LIKE '%DO NOT DELETE%' --FOR SQL Sentry 2.0 Alert 
		and (DATEDIFF(m, ISNULL(LastRun, '1900-01-01'), GETDATE()) > 12
		OR NextRun < GETDATE())
ORDER BY 3 DESC,4
OPTION(RECOMPILE);