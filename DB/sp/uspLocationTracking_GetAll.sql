-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspLocationTracking_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultLocationTracking
		AS
		(SELECT
				lt.[Id]
			   ,lt.[IPv4]
			   ,lt.[PcName]
			   ,u.UserName
			   ,p.FullName
			   ,p.PhoneNumber
			   ,lt.[CreatedDate]
			FROM LocationTracking AS lt
			LEFT JOIN [User] AS u
				ON u.Id = lt.CreatedBy
				AND u.IsDeleted = 0
			LEFT JOIN Person AS p
				ON p.Id = u.PersonId
				AND p.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR u.UserName LIKE CONCAT('%', @SearchText, '%')
			)
			AND lt.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultLocationTracking)
		SELECT
			*
		FROM TempResultLocationTracking r
			,TempCount
		ORDER BY 
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'pcName' THEN r.PcName
		END ASC,
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
				@SortField = 'phoneNumber' THEN r.PhoneNumber
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'iPv4' THEN r.IPv4
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'pcName' THEN r.PcName
		END DESC,
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
				@SortField = 'phoneNumber' THEN r.PhoneNumber
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'iPv4' THEN r.IPv4
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END DESC,
		--default
		CASE
			WHEN ISNULL(@SortOrder, '') = '' THEN r.CreatedDate
		END DESC
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
