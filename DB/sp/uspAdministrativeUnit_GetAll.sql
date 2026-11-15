-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspAdministrativeUnit_GetAll] (@SearchText NVARCHAR(MAX)
, @Page INT
, @PageSize INT
, @SortField NVARCHAR(MAX)
, @SortOrder NVARCHAR(MAX))
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		;
		WITH TempResultAdministrativeUnit
		AS
		(SELECT
				[Id]
			   ,[CodeNameVN]
			   ,[CodeNameEN]
			   ,[FullNameVN]
			   ,[FullNameEN]
			   ,[ShortNameVN]
			   ,[ShortNameEN]
			   ,[IsDefault]
			   ,[CreatedDate]
			FROM AdministrativeUnit
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR [CodeNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR [CodeNameEN] LIKE CONCAT('%', @SearchText, '%')
			OR [FullNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR [FullNameEN] LIKE CONCAT('%', @SearchText, '%')
			OR [ShortNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR [ShortNameEN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultAdministrativeUnit)
		SELECT
			*
		FROM TempResultAdministrativeUnit r
			,TempCount
		ORDER BY 
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'codeNameVN' THEN r.CodeNameVN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'codeNameEN' THEN r.CodeNameEN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fullNameVN' THEN r.FullNameVN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fullNameEN' THEN r.FullNameEN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'shortNameVN' THEN r.ShortNameVN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'shortNameEN' THEN r.ShortNameEN
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'codeNameVN' THEN r.CodeNameVN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'codeNameEN' THEN r.CodeNameEN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fullNameVN' THEN r.FullNameVN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fullNameEN' THEN r.FullNameEN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'shortNameVN' THEN r.ShortNameVN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'shortNameEN' THEN r.ShortNameEN
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
