-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspMedicineWarehouse_GetExpiry] (@Expiry INT
, @IsMain BIT
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
		WITH TempResultMedicineWarehouse
		AS
		(SELECT
				mw.[Id]
			   ,mw.[Name]
			   ,mw.[Quantity]
			   ,um.[Name] AS UnitMeasureName
			   ,mw.[Price]
			   ,mw.[Expiry]
			   ,mw.[Note]
			   ,mw.[CreatedDate]
			FROM MedicineWarehouse AS mw
			LEFT JOIN UnitMeasure AS um
				ON um.Id = mw.UnitMeasureId
				AND um.IsDeleted = 0
			WHERE DATEDIFF(MM, GETDATE(), mw.[Expiry]) <= @Expiry
			AND mw.IsMain = @IsMain
			AND mw.Quantity > 0
			AND mw.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultMedicineWarehouse)
		SELECT
			*
		FROM TempResultMedicineWarehouse r
			,TempCount
		ORDER BY
		--ascend 
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'name' THEN r.[Name]
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'unitMeasureName' THEN r.UnitMeasureName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'quantity' THEN r.Quantity
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'price' THEN r.Price
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'expiry' THEN r.Expiry
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'note' THEN r.Note
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'name' THEN r.[Name]
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'unitMeasureName' THEN r.UnitMeasureName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'quantity' THEN r.Quantity
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'price' THEN r.Price
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'expiry' THEN r.Expiry
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'note' THEN r.Note
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
