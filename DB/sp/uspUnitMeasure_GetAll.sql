-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUnitMeasure_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultUnitMeasure
		AS
		(SELECT
				um.[Id]
			   ,um.[Name]
			   ,um.[CreatedDate]
			FROM UnitMeasure AS um
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR um.[Name] LIKE CONCAT('%', @SearchText, '%')
			)
			AND um.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultUnitMeasure)
		SELECT
			*
		FROM TempResultUnitMeasure r
			,TempCount
		ORDER BY
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'name' THEN r.[Name]
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'name' THEN r.[Name]
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
