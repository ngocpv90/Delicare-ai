-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUserClaim_GetAll] (@UserId UNIQUEIDENTIFIER
, @UserGroupId UNIQUEIDENTIFIER
, @Page INT
, @PageSize INT)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		;
		WITH TempResultUserClaim
		AS
		(SELECT
				uc.[Id]
			   ,uc.[UserId]
			   ,u.UserName AS UserName
			   ,uc.[UserGroupId]
			   ,ug.[Name] AS UserGroupName
			   ,uc.[CreatedDate]
			FROM UserClaim AS uc
			LEFT JOIN [User] AS u
				ON u.Id = uc.[UserId]
				AND u.IsDeleted = 0
			LEFT JOIN UserGroup AS ug
				ON ug.Id = uc.[UserGroupId]
				AND ug.IsDeleted = 0
			WHERE (
			@UserId IS NULL
			OR uc.[UserId] = @UserId
			)
			AND (
			@UserGroupId IS NULL
			OR uc.[UserGroupId] = @UserGroupId
			)
			AND uc.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultUserClaim)
		SELECT
			*
		FROM TempResultUserClaim r
			,TempCount
		ORDER BY r.CreatedDate DESC OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
		OPTION (RECOMPILE);

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
