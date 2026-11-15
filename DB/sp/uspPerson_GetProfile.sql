-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPerson_GetProfile] (@Id UNIQUEIDENTIFIER)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		SELECT TOP 1
			p.Id
		   ,u.UserName
		   ,p.FullName
		   ,p.PhoneNumber
		   ,p.DateOfBirth
		   ,p.Sex
		   ,p.[Address]
		   ,p.Avatar
		FROM Person AS p
		LEFT JOIN [User] AS u
			ON u.PersonId = p.Id
				AND u.IsDeleted = 0
		WHERE u.Id = @Id
		AND u.IsActive = 1
		AND u.IsDeleted = 0

	END TRY
	BEGIN CATCH
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
