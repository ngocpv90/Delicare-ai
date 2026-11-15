-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspAdministrativeDistrict_GetAll] (@SearchText NVARCHAR(MAX)
, @AdministrativeProvinceId UNIQUEIDENTIFIER
, @AdministrativeUnitId UNIQUEIDENTIFIER
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
		WITH TempResultAdministrativeDistrict
		AS
		(SELECT
				ad.[Id]
			   ,ad.[Code]
			   ,ad.[CodeName]
			   ,ad.[NameVN]
			   ,ad.[NameEN]
			   ,ad.[FullNameVN]
			   ,ad.[FullNameEN]
			   ,ap.NameVN AS AdministrativeProvinceName
			   ,au.FullNameVN AS AdministrativeUnitName
			   ,ad.[IsDefault]
			   ,ad.[CreatedDate]
			FROM AdministrativeDistrict AS ad
			LEFT JOIN AdministrativeProvince AS ap
				ON ap.Id = ad.[AdministrativeProvinceId]
				AND ap.IsDeleted = 0
			LEFT JOIN AdministrativeUnit AS au
				ON au.Id = ad.[AdministrativeUnitId]
				AND au.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR ad.[Code] LIKE CONCAT('%', @SearchText, '%')
			OR ad.[CodeName] LIKE CONCAT('%', @SearchText, '%')
			OR ad.[NameVN] LIKE CONCAT('%', @SearchText, '%')
			OR ad.[NameEN] LIKE CONCAT('%', @SearchText, '%')
			OR ad.[FullNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR ad.[FullNameEN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			@AdministrativeProvinceId IS NULL
			OR ad.[AdministrativeProvinceId] = @AdministrativeProvinceId
			)
			AND (
			@AdministrativeUnitId IS NULL
			OR ad.[AdministrativeUnitId] = @AdministrativeUnitId
			)
			AND ad.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultAdministrativeDistrict)
		SELECT
			*
		FROM TempResultAdministrativeDistrict r
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
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
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
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
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
