-- =============================================
-- Author:      Sharon Rimer
-- Create date: 19/01/2016
-- Description: Generates a list of MDF, NDF, and LDF files that do not have a matching      
--              entry in the instance - that is, finds the orphaned ones.  You can exclude   
--              specific drives, and specific folders.
--              https://www.toadworld.com/platforms/sql-server/b/weblog/archive/2015/04/20/find-orphaned-mdf-ndf-and-ldf-files-on-all-drives
-- Prerequisite: 1. xp_cmdshell
-- =============================================
IF OBJECT_ID('tempdb..#OrphanedSQLFile') IS NULL
	CREATE TABLE #OrphanedSQLFile
		(
			OsFile VARCHAR(260),
			DtModified DATETIME NULL DEFAULT(''),
			SizeMB INT,
			DaysOld INT
		);
DECLARE	@IsLinux BIT;
SET @IsLinux = 0;
IF OBJECT_ID('sys.dm_os_host_info') IS NOT NULL
BEGIN
	SELECT	@IsLinux = 1
	FROM	sys.dm_os_host_info  
END
IF @IsLinux = 0
BEGIN

	DECLARE @GetInstances TABLE
		( Value nvarchar(100),
		InstanceNames nvarchar(100),
		Data nvarchar(100))

	Insert into @GetInstances
	EXECUTE xp_regread
		@rootkey = 'HKEY_LOCAL_MACHINE',
		@key = 'SOFTWARE\Microsoft\Microsoft SQL Server',
		@value_name = 'InstalledInstances';

	IF(Select COUNT(InstanceNames) from @GetInstances ) = 1
	BEGIN
		-- Get the set of fixed drives. Some drives you'll want to exclude. 
		IF OBJECT_ID('tempdb..#tmpDrives') IS NOT NULL
			DROP TABLE #tmpDrives;

		CREATE TABLE #tmpDrives
			(
				Drive CHAR(1) NOT NULL ,
				MBFreeUnused INT NOT NULL
			);

		INSERT  #tmpDrives EXEC xp_fixeddrives;

		DELETE  FROM #tmpDrives
		WHERE   Drive IN ( 'C' );

		-- Iterate through all the fixed drives, looking for database files. 
		-- Some files we'll want to delete. 
		IF OBJECT_ID('tempdb..#tmpOsFiles') IS NOT NULL
			DROP TABLE #tmpOsFiles;

		CREATE TABLE #tmpOsFiles
			(
				OsFile VARCHAR(260) NULL
			);

		DECLARE @Drive CHAR(1);
		DECLARE @Sql NVARCHAR(4000);

		DECLARE cur CURSOR LOCAL FAST_FORWARD
		FOR
			SELECT  Drive
			FROM    #tmpDrives;

		OPEN cur;
		FETCH NEXT FROM cur INTO @Drive;

		WHILE @@fetch_status = 0
		BEGIN
			--RAISERROR(@Drive, 10, 1) WITH NOWAIT;

			SET @Sql = 'dir ' + @Drive
				+ ':\*.mdf /a-d /a-h /a-s /a-r /b /s';
			INSERT  #tmpOsFiles
					EXEC xp_cmdshell @Sql;

			SET @Sql = 'dir ' + @Drive
				+ ':\*.ndf /a-d /a-h /a-s /a-r /b /s';
			INSERT  #tmpOsFiles
					EXEC xp_cmdshell @Sql;

			SET @Sql = 'dir ' + @Drive
				+ ':\*.ldf /a-d /a-h /a-s /a-r /b /s';
			INSERT  #tmpOsFiles
					EXEC xp_cmdshell @Sql;

			FETCH NEXT FROM cur INTO @Drive;
		END;

		CLOSE cur;
		DEALLOCATE cur;

		DELETE  FROM #tmpOsFiles
		WHERE   OsFile IS NULL
				OR OsFile = 'File Not Found'
				OR OsFile LIKE '%:\$RECYCLE_BIN\%' ESCAPE '~'; 
				--or OsFile like '%:\Program Files\Microsoft SQL Server%' escape '~' 
				--or OsFile like '%:\SW_DVD9_SQL_Svr_Enterprise_Edtn_2008_R2_English_MLF_X16-29540%' escape '~'

		-- For each file, get the date modified and the size.  The dir command gives 
		-- use a line like this:10/08/2013  02:37 PM            228253 TLXPVSQL01_TSS_ERD.png
		ALTER TABLE #tmpOsFiles ADD DtModified DATETIME NULL DEFAULT(''),SizeMB     INT      NULL DEFAULT('');

		DECLARE @Dir NVARCHAR(260);
		DECLARE @OsFile NVARCHAR(260);

		IF OBJECT_ID('tempdb..#tmpOsFileDetails') IS NOT NULL
			DROP TABLE #tmpOsFileDetails;

		CREATE TABLE #tmpOsFileDetails
			(
				OsFileDetails NVARCHAR(4000) NULL
			);

		DECLARE cur CURSOR LOCAL FAST_FORWARD
		FOR
			SELECT  OsFile
			FROM    #tmpOsFiles;

		OPEN cur;

		FETCH NEXT FROM cur INTO @OsFile;

		WHILE @@fetch_status = 0
		BEGIN
			SET @Sql = 'dir "' + @OsFile + '" /-c';
			INSERT  #tmpOsFileDetails
					EXEC xp_cmdshell @Sql;

			DELETE  FROM #tmpOsFileDetails
			WHERE   OsFileDetails IS NULL
					OR OsFileDetails LIKE '%Volume in drive % is %'
					OR OsFileDetails LIKE '%Volume Serial Number is %'
					OR OsFileDetails LIKE '%1 File(s) %'
					OR OsFileDetails LIKE '%0 Dir(s) %';

			SELECT  @Dir = RTRIM(LTRIM(REPLACE(OsFileDetails,
												'Directory of', ''))) + '\'
			FROM    #tmpOsFileDetails
			WHERE   OsFileDetails LIKE '%Directory of %';

			DELETE  FROM #tmpOsFileDetails
			WHERE   OsFileDetails LIKE '%Directory of %';
    
			UPDATE  #tmpOsFiles
			SET     DtModified = SUBSTRING(ofd.OsFileDetails, 1, 20) ,
					SizeMB = CAST(SUBSTRING(ofd.OsFileDetails, 21, 19) AS BIGINT)
					/ 1024 / 1024
			FROM    #tmpOsFileDetails ofd
					JOIN #tmpOsFiles os ON os.OsFile = @Dir
											+ SUBSTRING(ofd.OsFileDetails,
														40, 4000);

			DELETE  FROM #tmpOsFileDetails;

			FETCH NEXT FROM cur INTO @OsFile;
		END;

		CLOSE cur;
		DEALLOCATE cur;

		SELECT  os.OsFile,ISNULL(DtModified,'')DtModified,os.SizeMB,
				DATEDIFF(DAY, os.DtModified, GETDATE()) AS DaysOld
		FROM    master.sys.master_files mf
				RIGHT JOIN #tmpOsFiles os ON REPLACE(mf.physical_name,'\\','\') = REPLACE(os.OsFile,'\\','\')
		WHERE   mf.physical_name IS NULL
				AND RIGHT(os.OsFile, CHARINDEX('\', REVERSE(os.OsFile)) -1) NOT IN ('32veaiq8.mdf','ku5zi-bj.mdf','wakslhwn.ldf','w1ilinrh.ldf')
				AND LEFT(os.OsFile,LEN(os.OsFile) - charindex('\',reverse(os.OsFile),1) + 1) NOT LIKE '%Binn\Template\'
		ORDER BY os.SizeMB DESC
		OPTION(RECOMPILE);
	END
	ELSE
	BEGIN
		SELECT * FROM #OrphanedSQLFile;
	END


END
ELSE
BEGIN
	SELECT * FROM #OrphanedSQLFile;
END