-- =============================================
-- Author:		Sharon Rimer
-- Create date: 22/06/2016
-- Update date: 
-- Description:	Get configurations
-- =============================================
SELECT	SPCONFIGURE_Data.name ,
        SPCONFIGURE_Data.value config_value ,
        SPCONFIGURE_Data.value_in_use run_value
FROM	sys.configurations AS SPCONFIGURE_Data
OPTION (RECOMPILE); 

