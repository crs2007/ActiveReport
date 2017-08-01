-- =============================================
-- Author:		Sharon Rimer
-- Create Date: 11/05/2016
-- Update Date: 19/07/2017 Sharon add to @output filter "AND line NOT LIKE '%Hidden%' AND line NOT LIKE '%MB%'"
-- Description:	Get Volume Information 
-- Prerequisite: 1. xp_cmdshell
--				 2. powershell
-- Output:		#DiskInfo
-- =============================================
DECLARE	@IsLinux BIT;
SET @IsLinux = 0;
IF OBJECT_ID('sys.dm_os_host_info') IS NOT NULL
BEGIN
	SELECT	@IsLinux = 1
	FROM	sys.dm_os_host_info  
END
IF @IsLinux = 0
BEGIN
	--creating a temporary table for xp_cmdshell output
	DECLARE @output TABLE ( [ID] [int] NOT NULL IDENTITY(1,1) ,line VARCHAR(255) );
	IF OBJECT_ID('tempdb..#VolLabel') IS NOT NULL DROP TABLE #VolLabel;
	-- Setup Staging Area
	DECLARE	@Drives TABLE
	(
		Drive CHAR(1),
		Info VARCHAR(80)
	);
	-- Initialize Control Mechanism
	DECLARE	@Drive TINYINT;
	DECLARE @sql VARCHAR(1000)
	SET @sql = 'echo list volume | diskpart';
	SET	@Drive = 97;
	INSERT  @output 
	EXEC xp_cmdshell @sql;


	SELECT	
	SUBSTRING(line,15 + LEN(LTRIM(RTRIM(LEFT(REPLACE(line,'  Volume ',''),2)))),1) [Drive],

	SUBSTRING(line,19 + LEN(LTRIM(RTRIM(LEFT(REPLACE(line,'  Volume ',''),2)))),
	CHARINDEX('NTFS',SUBSTRING(line,19 + LEN(LTRIM(RTRIM(LEFT(REPLACE(line,'  Volume ',''),2)))),LEN(line)))-1

	) [Label]
	INTO	#VolLabel
	FROM	@output
	WHERE	line LIKE '%Volume%'
			AND	line LIKE '%Partition%'
			AND	line NOT LIKE '%Hidden%'
			AND	line NOT LIKE '%MB%'
			AND ID > 8;

	WHILE @Drive <= 122
	BEGIN
		SET	@sql = 'EXEC xp_cmdshell ''fsutil volume diskfree ' + CHAR(@Drive) + ':'''
		
		INSERT	@Drives
			(
				Info
			)
		EXEC	(@sql)

		UPDATE	@Drives
		SET	Drive = CHAR(@Drive)
		WHERE	Drive IS NULL

		SET	@Drive = @Drive + 1
	END

	-- Show the expected output
	SELECT	d.Drive,
			SUM(CASE WHEN Info LIKE 'Total # of bytes             : %' THEN CAST(REPLACE(SUBSTRING(Info, 32, 48), CHAR(13), '') AS BIGINT) ELSE CAST(0 AS BIGINT) END) /1024/1024/1024 AS [Total_Size],
			SUM(CASE WHEN Info LIKE 'Total # of free bytes        : %' THEN CAST(REPLACE(SUBSTRING(Info, 32, 48), CHAR(13), '') AS BIGINT) ELSE CAST(0 AS BIGINT) END) /1024/1024/1024 AS [Free_Space],
			v.Label
	FROM	(	SELECT	Drive, Info
				FROM	@Drives
				WHERE	Info LIKE 'Total # of %') AS d
			LEFT JOIN #VolLabel v ON v.Drive = d.Drive
	GROUP BY	d.Drive,v.Label
	HAVING SUM(CASE WHEN Info LIKE 'Total # of bytes             : %' THEN CAST(REPLACE(SUBSTRING(Info, 32, 48), CHAR(13), '') AS BIGINT) ELSE CAST(0 AS BIGINT) END) /1024/1024/1024 > 0
	ORDER BY	d.Drive;

END