-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspAdministrativeRegion_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultAdministrativeRegion
		AS
		(SELECT
				[Id]
			   ,[CodeVN]
			   ,[CodeEN]
			   ,[NameVN]
			   ,[NameEN]
			   ,[IsDefault]
			   ,[CreatedDate]
			FROM AdministrativeRegion
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR [CodeVN] LIKE CONCAT('%', @SearchText, '%')
			OR [CodeEN] LIKE CONCAT('%', @SearchText, '%')
			OR [NameVN] LIKE CONCAT('%', @SearchText, '%')
			OR [NameEN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultAdministrativeRegion)
		SELECT
			*
		FROM TempResultAdministrativeRegion r
			,TempCount
		ORDER BY
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'codeVN' THEN r.CodeVN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'codeEN' THEN r.CodeEN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'nameVN' THEN r.NameVN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'nameEN' THEN r.NameEN
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'codeVN' THEN r.CodeVN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'codeEN' THEN r.CodeEN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'nameVN' THEN r.NameVN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'nameEN' THEN r.NameEN
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
