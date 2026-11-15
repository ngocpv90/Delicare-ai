-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspAdministrativeProvince_GetAll] (@SearchText NVARCHAR(MAX)
, @AdministrativeUnitId UNIQUEIDENTIFIER
, @AdministrativeRegionId UNIQUEIDENTIFIER
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
		WITH TempResultAdministrativeProvince
		AS
		(SELECT
				ap.[Id]
			   ,ap.[Code]
			   ,ap.[CodeName]
			   ,ap.[NameVN]
			   ,ap.[NameEN]
			   ,ap.[FullNameVN]
			   ,ap.[FullNameEN]
			   ,au.FullNameVN AS AdministrativeUnitName
			   ,ar.NameVN AS AdministrativeRegionName
			   ,ap.[IsDefault]
			   ,ap.[CreatedDate]
			FROM AdministrativeProvince AS ap
			LEFT JOIN AdministrativeUnit AS au
				ON au.Id = ap.[AdministrativeUnitId]
				AND au.IsDeleted = 0
			LEFT JOIN AdministrativeRegion AS ar
				ON ar.Id = ap.[AdministrativeRegionId]
				AND ar.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR ap.[Code] LIKE CONCAT('%', @SearchText, '%')
			OR ap.[CodeName] LIKE CONCAT('%', @SearchText, '%')
			OR ap.[NameVN] LIKE CONCAT('%', @SearchText, '%')
			OR ap.[NameEN] LIKE CONCAT('%', @SearchText, '%')
			OR ap.[FullNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR ap.[FullNameEN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			@AdministrativeUnitId IS NULL
			OR ap.[AdministrativeUnitId] = @AdministrativeUnitId
			)
			AND (
			@AdministrativeRegionId IS NULL
			OR ap.[AdministrativeRegionId] = @AdministrativeRegionId
			)
			AND ap.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultAdministrativeProvince)
		SELECT
			*
		FROM TempResultAdministrativeProvince r
			,TempCount
		ORDER BY
		--ascend 
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'code' THEN r.Code
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'codeName' THEN r.CodeName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'nameVN' THEN r.NameVN
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'nameEN' THEN r.NameEN
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
				@SortField = 'administrativeUnitName' THEN r.AdministrativeUnitName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'administrativeRegionName' THEN r.AdministrativeRegionName
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'code' THEN r.Code
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'codeName' THEN r.CodeName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'nameVN' THEN r.NameVN
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'nameEN' THEN r.NameEN
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
				@SortField = 'administrativeUnitName' THEN r.AdministrativeUnitName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'administrativeRegionName' THEN r.AdministrativeRegionName
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
