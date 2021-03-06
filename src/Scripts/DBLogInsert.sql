CREATE PROCEDURE [dbo].[LogInsert]
		@Date      		[datetime],
    	@Level     		[tinyint],
    	@Context   		[nvarchar](max)		= NULL,
    	@Class     		[nvarchar](255)		= NULL,
    	@Method    		[nvarchar](255)		= NULL,
    	@FilePath  		[nvarchar](max)		= NULL,
    	@LineNumber		[int]				= NULL,
    	@Message   		[nvarchar](max),
    	@Exception 		[nvarchar](max)		= NULL,
    	@StackSources	[nvarchar](max)		= NULL,
		@Namespace 		[nvarchar](max)		= NULL,
		@Assembly  		[nvarchar](max)		= NULL,
		@MachineName	[nvarchar](255)		= NULL,
		@MachineIp		[nvarchar](64)		= NULL,
		@ProcessName	[nvarchar](max)		= NULL,
		@ProcessId 		[int]				= NULL
AS
	INSERT INTO [dbo].[Logs]
			(				
			 [Date]      		
			,[Level]     		
			,[Context]   		
			,[Class]     		
			,[Method]    		
			,[FilePath]  		
			,[LineNumber]		
			,[Message]   		
			,[Exception] 		
			,[StackSources]		
			,[Namespace] 		
			,[Assembly]  		
			,[MachineName]
			,[MachineIp]		
			,[ProcessName]		
			,[ProcessId] 		
		   )
     VALUES
           (
		    @Date      		
		   ,@Level     		
		   ,@Context   		
		   ,@Class     		
		   ,@Method    		
		   ,@FilePath  		
		   ,@LineNumber		
		   ,@Message   		
		   ,@Exception 		
		   ,@StackSources	
		   ,@Namespace 		
		   ,@Assembly  		
		   ,@MachineName	
		   ,@MachineIp
		   ,@ProcessName	
		   ,@ProcessId 		
		   );
