-- =============================================
-- Author:      Sharon
-- Create date: 23/06/2016
-- Description: #OrphanedUser
-- =============================================
IF OBJECT_ID('tempdb..#Users') IS NOT NULL DROP TABLE #Users;
CREATE TABLE #Users
    (
        DatabaseName sysname ,
        Type NVARCHAR(260) ,
        sid NVARCHAR(MAX) ,
        UserName sysname
    );
IF ( SELECT SERVERPROPERTY('ProductMajorVersion')) >= '10'
BEGIN
EXEC sp_MSforeachdb '
INSERT #Users
SELECT ''?'',dp.type_desc, convert(nvarchar(max),dp.SID,1), dp.name AS user_name  
FROM   [?].sys.database_principals AS dp  
       LEFT JOIN [?].sys.server_principals AS sp ON dp.SID = sp.SID  
WHERE  sp.SID IS NULL
       AND authentication_type_desc = ''INSTANCE''
OPTION(RECOMPILE);';
END
ELSE
BEGIN
    EXEC sp_MSforeachdb '
INSERT #Users
SELECT ''?'',dp.type_desc, [sys].[fn_varbintohexstr](dp.sid), dp.name AS user_name  
FROM   [?].sys.database_principals AS dp  
       LEFT JOIN [?].sys.server_principals AS sp ON dp.SID = sp.SID  
WHERE  sp.SID IS NULL
OPTION(RECOMPILE);';
END
	
SELECT  'Login ' + QUOTENAME(SP.name) + ' Have a user on DB - '
        + QUOTENAME(U.DatabaseName) + ' with a different sid.' [Text]
FROM    sys.server_principals SP
        INNER JOIN #Users U ON U.UserName = SP.name
WHERE   SP.sid != U.sid
		AND SP.name != 'public'
OPTION(RECOMPILE);

DROP TABLE #Users;