-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUser_GetAll] (@FullName NVARCHAR(MAX)
, @UserName NVARCHAR(MAX)
, @UserGroupId NVARCHAR(MAX)
, @IsActive BIT = NULL -- T45R.8 (FE-1): loc Dang dung / Da khoa (NULL = tat ca)
, @Page INT
, @PageSize INT
, @SortField NVARCHAR(MAX)
, @SortOrder NVARCHAR(MAX))
AS
BEGIN
	SET NOCOUNT ON;
	-- Rà soát BE: chặn PageSize / Page bất thường (như các SP GetAll khác) – PageSize=1000000 từng trả cả bảng.
	IF @Page IS NULL OR @Page < 1 SET @Page = 1;
	IF @PageSize IS NULL OR @PageSize < 1 SET @PageSize = 10;
	IF @PageSize > 1000 SET @PageSize = 1000;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		;
		WITH TempResultUser
		AS
		(SELECT
				u.Id
			   ,u.UserName
			   ,p.FullName
			   ,u.PersonId
			   ,u.IsActive
			   ,u.IsAdmin
			   ,u.IsDefault
			   ,u.CreatedDate
			FROM [User] AS u
			LEFT JOIN Person AS p
				ON p.Id = u.PersonId
				AND p.IsDeleted = 0
			LEFT JOIN UserClaim AS uc
				ON uc.UserId = u.Id
				AND @UserGroupId IS NOT NULL
				AND uc.IsDeleted = 0
			WHERE (
			ISNULL(@FullName, '') = ''
			OR p.FullName LIKE CONCAT('%', @FullName, '%')
			)
			AND (
			@UserGroupId IS NULL
			OR uc.UserGroupId = @UserGroupId
			)
			AND (
			ISNULL(@UserName, '') = ''
			OR u.UserName LIKE CONCAT('%', @UserName, '%')
			)
			AND (
			@IsActive IS NULL
			OR u.IsActive = @IsActive
			)
			AND u.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultUser)
		SELECT
			*
		FROM TempResultUser r
			,TempCount
		ORDER BY 
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'userName' THEN r.UserName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fullName' THEN r.FullName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'isAdmin' THEN r.IsAdmin
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'isActive' THEN r.IsActive
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'userName' THEN r.UserName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fullName' THEN r.FullName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'isAdmin' THEN r.IsAdmin
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'isActive' THEN r.IsActive
		END DESC,
		--default
		r.CreatedDate DESC 
		OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
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
