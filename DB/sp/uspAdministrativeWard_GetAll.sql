-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspAdministrativeWard_GetAll] (@SearchText NVARCHAR(MAX)
, @AdministrativeDistrictId UNIQUEIDENTIFIER
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
		WITH TempResultAdministrativeWard
		AS
		(SELECT
				aw.[Id]
			   ,aw.[Code]
			   ,aw.[CodeName]
			   ,aw.[NameVN]
			   ,aw.[NameEN]
			   ,aw.[FullNameVN]
			   ,aw.[FullNameEN]
			   ,ad.NameVN AS AdministrativeDistrictName
			   ,au.FullNameVN AS AdministrativeUnitName
			   ,aw.[IsDefault]
			   ,aw.[CreatedDate]
			FROM AdministrativeWard AS aw
			LEFT JOIN AdministrativeDistrict AS ad
				ON ad.Id = aw.[AdministrativeDistrictId]
				AND ad.IsDeleted = 0
			LEFT JOIN AdministrativeUnit AS au
				ON au.Id = aw.[AdministrativeUnitId]
				AND au.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR aw.[Code] LIKE CONCAT('%', @SearchText, '%')
			OR aw.[CodeName] LIKE CONCAT('%', @SearchText, '%')
			OR aw.[NameVN] LIKE CONCAT('%', @SearchText, '%')
			OR aw.[NameEN] LIKE CONCAT('%', @SearchText, '%')
			OR aw.[FullNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR aw.[FullNameEN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			@AdministrativeDistrictId IS NULL
			OR aw.[AdministrativeDistrictId] = @AdministrativeDistrictId
			)
			AND (
			@AdministrativeUnitId IS NULL
			OR aw.[AdministrativeUnitId] = @AdministrativeUnitId
			)
			AND aw.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultAdministrativeWard)
		SELECT
			*
		FROM TempResultAdministrativeWard r
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
				@SortField = 'administrativeDistrictName' THEN r.AdministrativeDistrictName
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
				@SortField = 'administrativeDistrictName' THEN r.AdministrativeDistrictName
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
