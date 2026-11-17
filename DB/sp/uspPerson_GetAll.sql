-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPerson_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultPerson
		AS
		(SELECT
				p.[Id]
			   ,p.FullName
			   ,p.PhoneNumber
			   ,p.DateOfBirth
			   ,p.Sex
			   ,p.[CreatedDate]
			FROM Person AS p
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR p.FullName LIKE CONCAT('%', @SearchText, '%')
			)
			AND p.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultPerson)
		SELECT
			*
		FROM TempResultPerson r
			,TempCount
		ORDER BY 
		--ascend
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
				@SortField = 'dateOfBirth' THEN r.DateOfBirth
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'sex' THEN r.Sex
		END ASC,
		--descend
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
				@SortField = 'dateOfBirth' THEN r.DateOfBirth
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'sex' THEN r.Sex
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
