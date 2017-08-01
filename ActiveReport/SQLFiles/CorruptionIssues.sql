-- =============================================
-- Author:		Sharon Rimer
-- Create date: 19/05/2016
-- Description:	Get Corruption Issues
-- Prerequisite: None
-- =============================================
SELECT  SD.name AS DatabaseName ,
		MSP.file_id AS FileID ,
		SMF.physical_name AS PhysicalFilePath ,
		MSP.page_id AS PageID ,
		CASE WHEN MSP.event_type = 1
				THEN '823 error caused by an operating system CRC error or 824 error other than a bad checksum or a torn page'
				WHEN MSP.event_type = 2 THEN 'Bad checksum'
				WHEN MSP.event_type = 3 THEN 'Torn Page'
				WHEN MSP.event_type = 4
				THEN 'Restored (The page was restored after it was marked bad)'
				WHEN MSP.event_type = 5 THEN 'Repaired (DBCC repaired the page)'
				WHEN MSP.event_type = 7 THEN 'Deallocated by DBCC'
		END AS EventDescription ,
		MSP.error_count AS ErrorCount ,
		MSP.last_update_date AS LastUpdated
FROM    msdb..suspect_pages MSP
		INNER JOIN sys.databases SD ON SD.database_id = MSP.database_id
		INNER JOIN sys.master_files SMF ON SMF.database_id = MSP.database_id
											AND SMF.file_id = MSP.file_id
WHERE	last_update_date > DATEADD(DAY,7,GETDATE())
OPTION(RECOMPILE);