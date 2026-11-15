-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspSystem_BackupDB]
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		 -- Ngắt kết nối database đích
		ALTER DATABASE [AppDelicare]
		SET SINGLE_USER
		WITH ROLLBACK IMMEDIATE;

		-- Restore database AppKitchen sang AppDelicare
		RESTORE DATABASE [AppDelicare]
		FROM DISK = N'D:\Duan\SourceCode\backup\AppKitchen.bak'
		WITH
			MOVE N'KetchenApp'
			TO N'D:\Duan\Delicare\Delicare-ai\backup-db\AppDelicare_20261110.mdf',

			MOVE N'KetchenApp_log'
			TO N'D:\Duan\Delicare\Delicare-ai\backup-db\AppDelicare_20261110_log.ldf',

			REPLACE,
			RECOVERY,
			STATS = 10;

		-- Mở lại kết nối
		ALTER DATABASE [AppDelicare]
		SET MULTI_USER;

		PRINT N'Restore AppDelicare thành công!';

	END TRY
	BEGIN CATCH
		 -- Đảm bảo database trở lại MULTI_USER nếu có lỗi
		IF DB_ID('AppDelicare') IS NOT NULL
		BEGIN
			ALTER DATABASE [AppDelicare]
			SET MULTI_USER WITH ROLLBACK IMMEDIATE;
		END;

		PRINT ERROR_MESSAGE();

		SELECT
			@strErrorMessage = ERROR_MESSAGE()
			+ ' Line:'
			+ CONVERT(VARCHAR(5), ERROR_LINE())
		   ,@intErrorSeverity = ERROR_SEVERITY()
		   ,@intErrorState = ERROR_STATE();
		RAISERROR (
		@strErrorMessage,   -- Message text.
		@intErrorSeverity,  -- Severity.
		@intErrorState      -- State.
		);
	END CATCH;
END
GO
