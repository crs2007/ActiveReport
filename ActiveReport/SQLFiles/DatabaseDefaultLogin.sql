-- =============================================
-- Author:		Sharon Rimer
-- Create date: 24/11/2016
-- Description:	Get SQL Login That not Match to other servers.
-- Prerequisite: None
-- Output:		#DatabaseDefaultLogin
-- =============================================
SELECT	REPLACE(type_desc COLLATE DATABASE_DEFAULT,'_',' ') + ' - ' + name COLLATE DATABASE_DEFAULT + ' have connection to "default_database_name" - ' + QUOTENAME(default_database_name COLLATE DATABASE_DEFAULT) + ', that no longer exists on this server.' [msg]
FROM	sys.server_principals
WHERE	default_database_name NOT IN (SELECT name FROM	sys.databases)
OPTION(RECOMPILE);