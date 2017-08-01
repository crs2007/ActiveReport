-- =============================================
-- Author:		Dror
-- Create date: 26/05/2016
-- Description:	Get Installed Programs
-- Prerequisite: None
-- Output:		#InstalledProg
-- =============================================
IF OBJECT_ID('tempdb..#InstalledProg') IS NULL 
	CREATE TABLE #InstalledProg
    (
        Name VARCHAR(255) ,
        Vendor VARCHAR(255) ,
        [version] VARCHAR(255)
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
	CREATE TABLE #InstalledProgTemp
		(
			id INT IDENTITY ,
			output VARCHAR(255)
		);
	INSERT  INTO #InstalledProgTemp
			EXEC xp_cmdshell 'wmic product get name,vendor,version';
		WITH    FormSplitXML
					AS ( SELECT   id ,
								output ,
								CONVERT(XML, '<r><n>'
								+ REPLACE(output, '   ', '</n><n>')
								+ '</n></r>') AS X
						FROM     #InstalledProgTemp
						),
	--select * from FormSplitXML
				rownumXML
					AS ( SELECT   t1.id ,
								LTRIM(RTRIM(t1.columnvalue)) AS columnvalue ,
								ROW_NUMBER() OVER ( PARTITION BY t1.id ORDER BY t1.columnname ) AS rownumber
						FROM     ( SELECT    id ,
											i.value('local-name(.)','varchar(255)') columnname ,
											i.value('.','varchar(255)') columnvalue 
									FROM      FormSplitXML Spt
											CROSS APPLY Spt.X.nodes('//*[text()]') x ( i )
								) t1
						)
		--select * from rownumXML

	INSERT  INTO #InstalledProg
		SELECT  [1] AS Name ,
				[2] AS vendor ,
				[3] AS [version]
		FROM    ( SELECT    id ,
							columnvalue ,
							rownumber
					FROM      rownumXML
				) t1 PIVOT ( MAX(columnvalue) FOR rownumber IN ( [1], [2], [3] ) )
	AS pvt;

	DROP TABLE #InstalledProgTemp; 
	DELETE  FROM #InstalledProg
	WHERE   Name = 'name';

END 
SELECT	Name,
		Vendor,
		version
FROM	#InstalledProg
OPTION(RECOMPILE);

DROP TABLE #InstalledProg; 
