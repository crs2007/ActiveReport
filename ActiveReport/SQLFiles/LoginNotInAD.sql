-- =============================================
-- Author:		Sharon Rimer
-- Create date: 30/06/2016
-- Update date: 30/07/2017 Sharon Rimer change xp_logininfo to sp_validatelogins
-- Description:	Get Login That not Exists on AD.
-- Prerequisite: None
-- Output:		#LoginNotInAD
-- =============================================
IF OBJECT_ID('tempdb..#LoginNotInAD') IS NULL
    CREATE TABLE #LoginNotInAD
    (	[sid]  VARBINARY(85),
        [LoginName] sysname NULL
    );
    

INSERT #LoginNotInAD
EXEC sys.sp_validatelogins;

SELECT	LNIA.LoginName
FROM	#LoginNotInAD LNIA
WHERE	LNIA.LoginName NOT IN (N'NT Service\MSSQLSERVER')
OPTION(RECOMPILE);

DROP TABLE #LoginNotInAD;