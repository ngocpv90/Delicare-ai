-- Stored procedures used by AppDelicare WebApi.
-- Re-runnable (CREATE OR ALTER). Extracted from db-structure-appdelicare.sql (2026-11-12).
-- uspSystem_BackupDB intentionally excluded: it restores/overwrites a database from a local disk path.
-- uspMedicalExamination_GetAll: added optional filter params (PriceFrom/To, CreatedDateFrom/To).
USE [Delicare]
GO

/****** uspAdministrativeDistrict_GetAll ******/
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

/****** uspAdministrativeProvince_GetAll ******/
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

/****** uspAdministrativeRegion_GetAll ******/
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

/****** uspAdministrativeUnit_GetAll ******/
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

/****** uspAdministrativeWard_GetAll ******/
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

/****** uspAuditChange_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspAuditChange_GetAll] (@IdChange UNIQUEIDENTIFIER
, @Action NVARCHAR(MAX)
, @EntityName NVARCHAR(MAX)
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
		WITH TempResultAuditChange
		AS
		(SELECT
				ac.[Id]
			   ,ac.[IdChange]
			   ,ac.[Action]
			   ,ac.[EntityName]
			   ,ac.[Changes]
			   ,u.UserName
			   ,ac.[CreatedDate]
			FROM AuditChange AS ac
			LEFT JOIN [User] AS u
				ON u.Id = ac.CreatedBy
				AND u.IsDeleted = 0
			WHERE (
			@IdChange IS NULL
			OR ac.[IdChange] = @IdChange
			)
			AND (
			ISNULL(@Action, '') = ''
			OR ac.[Action] = @Action
			)
			AND (
			ISNULL(@EntityName, '') = ''
			OR ac.[EntityName] = @EntityName
			)
			AND ac.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultAuditChange)
		SELECT
			*
		FROM TempResultAuditChange r
			,TempCount
		ORDER BY
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'idChange' THEN r.IdChange
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'action' THEN r.[Action]
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'entityName' THEN r.EntityName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'changes' THEN r.[Changes]
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'userName' THEN r.UserName
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'idChange' THEN r.IdChange
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'action' THEN r.[Action]
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'entityName' THEN r.EntityName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'changes' THEN r.[Changes]
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'userName' THEN r.UserName
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

/****** uspLocationTracking_DeleteData ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspLocationTracking_DeleteData]
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		DELETE FROM AuditChange;
		DELETE FROM LocationTracking;

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

/****** uspLocationTracking_GetAll ******/
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

/****** uspMedicalExamination_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspMedicalExamination_GetAll] (@SearchText NVARCHAR(MAX)
, @PriceFrom DECIMAL(18, 2) = NULL
, @PriceTo DECIMAL(18, 2) = NULL
, @CreatedDateFrom DATE = NULL
, @CreatedDateTo DATE = NULL
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
		WITH TempResultMedicalExamination
		AS
		(SELECT
				me.[Id]
			   ,me.[Name]
			   ,me.[Price]
			   ,me.[NormalIndex]
			   ,me.[CreatedDate]
			FROM MedicalExamination AS me
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR me.[Name] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (@PriceFrom IS NULL OR me.[Price] >= @PriceFrom)
			AND (@PriceTo IS NULL OR me.[Price] <= @PriceTo)
			AND (@CreatedDateFrom IS NULL OR me.[CreatedDate] >= @CreatedDateFrom)
			AND (@CreatedDateTo IS NULL OR me.[CreatedDate] < DATEADD(DAY, 1, @CreatedDateTo))
			AND me.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultMedicalExamination)
		SELECT
			*
		FROM TempResultMedicalExamination r
			,TempCount
		ORDER BY
		--ascend 
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'name' THEN r.[Name]
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'price' THEN r.Price
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'normalIndex' THEN r.NormalIndex
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'name' THEN r.[Name]
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'price' THEN r.Price
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'normalIndex' THEN r.NormalIndex
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

/****** uspMedicineWarehouse_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspMedicineWarehouse_GetAll] (@SearchText NVARCHAR(MAX)
, @SupplierId UNIQUEIDENTIFIER
, @Quantity INT
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
			   ,mw.[SupplierId]
			   ,s.[Name] AS SupplierName
			   ,mw.[UnitMeasureId]
			   ,um.[Name] AS UnitMeasureName
			   ,mw.[PriceInput]
			   ,mw.[DateInput]
			   ,mw.[Price]
			   ,mw.[Expiry]
			   ,mw.[PriceTotal]
			   ,mw.[Note]
			   ,mw.[IsMain]
			   ,mw.[CreatedDate]
			FROM MedicineWarehouse AS mw
			LEFT JOIN Supplier AS s
				ON s.Id = mw.SupplierId
				AND s.IsDeleted = 0
			LEFT JOIN UnitMeasure AS um
				ON um.Id = mw.UnitMeasureId
				AND um.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR mw.[Name] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			@SupplierId IS NULL
			OR mw.[SupplierId] = @SupplierId
			)
			AND (
			CASE
				WHEN @Quantity > 0 THEN mw.Quantity
				ELSE 1
			END > 0
			)
			AND mw.IsMain = @IsMain
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
				@SortField = 'supplierName' THEN r.SupplierName
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
				@SortField = 'supplierName' THEN r.SupplierName
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

/****** uspMedicineWarehouse_GetExpiry ******/
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

/****** uspObjectMenu_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspObjectMenu_GetAll] (@SearchText NVARCHAR(MAX)
, @ParentId UNIQUEIDENTIFIER
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
		WITH TempResultObjectMenu
		AS
		(SELECT
				om.Id
			   ,om.[Name]
			   ,om.ShortName
			   ,om1.[Name] AS ParentName
			   ,om.MetaLink
			   ,om.IconName
			   ,om.LevelShow
			   ,om.OrderShow
			   ,om.DataTarget
			   ,om.AriaControls
			   ,om.IsActive
			   ,om.IsDefault
			   ,om.CreatedDate
			FROM ObjectMenu AS om
			LEFT JOIN ObjectMenu AS om1
				ON om1.Id = om.ParentId
				AND om1.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR om.[Name] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			@ParentId IS NULL
			OR om.ParentId = @ParentId
			)
			AND om.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultObjectMenu)
		SELECT
			*
		FROM TempResultObjectMenu r
			,TempCount
		ORDER BY
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'name' THEN r.[Name]
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'shortName' THEN r.ShortName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'parentName' THEN r.ParentName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'metaLink' THEN r.MetaLink
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'iconName' THEN r.IconName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'levelShow' THEN r.LevelShow
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'orderShow' THEN r.OrderShow
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'dataTarget' THEN r.DataTarget
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'ariaControls' THEN r.AriaControls
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'isActive' THEN r.IsActive
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'name' THEN r.[Name]
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'shortName' THEN r.ShortName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'parentName' THEN r.ParentName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'metaLink' THEN r.MetaLink
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'iconName' THEN r.IconName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'levelShow' THEN r.LevelShow
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'orderShow' THEN r.OrderShow
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'dataTarget' THEN r.DataTarget
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'ariaControls' THEN r.AriaControls
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

/****** uspPatient_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPatient_GetAll] (@SearchText NVARCHAR(MAX)
, @PhoneNumber NVARCHAR(MAX)
, @CreatedDate DATETIME
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
		WITH TempResultPatient
		AS
		(SELECT
				p.[Id]
			   ,p.[FullName]
			   ,p.PhoneNumber
			   ,p.DateOfBirth
			   ,p.[Sex]
			   ,ap.NameVN AS AdministrativeProvinceName
			   ,ad.NameVN AS AdministrativeDistrictName
			   ,aw.NameVN AS AdministrativeWardName
			   ,p.[Address]
			   ,p1.[FullName] AS PersonName
			   ,p.[CreatedDate]
			FROM Patient AS p
			LEFT JOIN AdministrativeProvince AS ap
				ON ap.Id = p.AdministrativeProvinceId
				AND ap.IsDeleted = 0
			LEFT JOIN AdministrativeDistrict AS ad
				ON ad.Id = p.AdministrativeDistrictId
				AND ad.IsDeleted = 0
			LEFT JOIN AdministrativeWard AS aw
				ON aw.Id = p.AdministrativeWardId
				AND aw.IsDeleted = 0
			LEFT JOIN [User] AS u
				ON u.Id = p.CreatedBy
				AND u.IsDeleted = 0
			LEFT JOIN Person AS p1
				ON p1.Id = u.PersonId
				AND u.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR p.FullName LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			ISNULL(@PhoneNumber, '') = ''
			OR p.PhoneNumber LIKE CONCAT('%', @PhoneNumber, '%')
			)
			AND (
			ISNULL(@CreatedDate, '') = ''
			OR CONVERT(VARCHAR(10), p.CreatedDate, 103) = CONVERT(VARCHAR(10), @CreatedDate, 103)
			)
			AND p.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultPatient)
		SELECT
			*
		FROM TempResultPatient r
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
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
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
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
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

/****** uspPatientDetail_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPatientDetail_GetAll] (@SearchText NVARCHAR(MAX)
, @PhoneNumber NVARCHAR(MAX)
, @CreatedDate DATETIME
, @PersonId UNIQUEIDENTIFIER
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
		WITH TempResultPatient
		AS
		(SELECT
				pd.[Id]
			   ,p.[FullName]
			   ,p.PhoneNumber
			   ,p.DateOfBirth
			   ,p.[Sex]
			   ,ap.NameVN AS AdministrativeProvinceName
			   ,ad.NameVN AS AdministrativeDistrictName
			   ,aw.NameVN AS AdministrativeWardName
			   ,p.[Address]
			   ,p1.[FullName] AS PersonName
			   ,p.[CreatedDate]
			   ,pd.[Result]
			FROM PatientDetail AS pd
			LEFT JOIN Patient AS p
				ON p.Id = pd.PatientId
				AND p.IsDeleted = 0
			LEFT JOIN AdministrativeProvince AS ap
				ON ap.Id = p.AdministrativeProvinceId
				AND ap.IsDeleted = 0
			LEFT JOIN AdministrativeDistrict AS ad
				ON ad.Id = p.AdministrativeDistrictId
				AND ad.IsDeleted = 0
			LEFT JOIN AdministrativeWard AS aw
				ON aw.Id = p.AdministrativeWardId
				AND aw.IsDeleted = 0
			LEFT JOIN [User] AS u
				ON u.Id = p.CreatedBy
				AND u.IsDeleted = 0
			LEFT JOIN Person AS p1
				ON p1.Id = u.PersonId
				AND u.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR p.FullName LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			ISNULL(@PhoneNumber, '') = ''
			OR p.PhoneNumber LIKE CONCAT('%', @PhoneNumber, '%')
			)
			AND (
			ISNULL(@CreatedDate, '') = ''
			OR CONVERT(VARCHAR(10), p.CreatedDate, 103) = CONVERT(VARCHAR(10), @CreatedDate, 103)
			)
			AND pd.PersonId = @PersonId
			AND pd.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultPatient)
		SELECT
			*
		FROM TempResultPatient r
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
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'result' THEN r.Result
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
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'result' THEN r.Result
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

/****** uspPerson_GetAll ******/
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

/****** uspPerson_GetAllDataByKey ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPerson_GetAllDataByKey] (@Key NVARCHAR(MAX))
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY
		SELECT
			p.[Id]
		   ,p.FullName
		FROM Person AS p
		WHERE (
		ISNULL(@Key, '') = ''
		OR p.FullName LIKE CONCAT('%', @Key, '%')
		)
		AND p.IsDeleted = 0

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

/****** uspPerson_GetProfile ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPerson_GetProfile] (@Id UNIQUEIDENTIFIER)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		SELECT TOP 1
			p.Id
		   ,u.UserName
		   ,p.FullName
		   ,p.PhoneNumber
		   ,p.DateOfBirth
		   ,p.Sex
		   ,p.[Address]
		   ,p.Avatar
		FROM Person AS p
		LEFT JOIN [User] AS u
			ON u.PersonId = p.Id
				AND u.IsDeleted = 0
		WHERE u.Id = @Id
		AND u.IsActive = 1
		AND u.IsDeleted = 0

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

/****** uspSupplier_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspSupplier_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultSupplier
		AS
		(SELECT
				s.[Id]
			   ,s.[Name]
			   ,s.[Note]
			   ,s.[CreatedDate]
			FROM Supplier AS s
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR s.[Name] LIKE CONCAT('%', @SearchText, '%')
			)
			AND s.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultSupplier)
		SELECT
			*
		FROM TempResultSupplier r
			,TempCount
		ORDER BY
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'name' THEN r.[Name]
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

/****** uspSystem_ExecuteQuery ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspSystem_ExecuteQuery] (@StrSql NVARCHAR(MAX))
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		EXECUTE (@StrSql)

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

/****** uspSystem_GetColumnNameForeignKey ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspSystem_GetColumnNameForeignKey]
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		SELECT
			c.name AS ColumnName
		   ,CONCAT('[', t.name, ']') AS TableName
		FROM sys.columns AS c
		JOIN sys.tables AS t
			ON c.object_id = t.object_id
		WHERE c.name LIKE '%Id%'
		AND c.name <> 'Id'
		ORDER BY TableName
		, ColumnName;

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

/****** uspTableName_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspTableName_GetAll]
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		SELECT
			CONCAT('[', TABLE_NAME, ']') AS EntityName
		FROM INFORMATION_SCHEMA.TABLES
		WHERE TABLE_TYPE = 'BASE TABLE'
		AND TABLE_NAME NOT IN ('AuditChange', 'LocationTracking')
		ORDER BY TABLE_NAME ASC

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

/****** uspTestDemo_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspTestDemo_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultTestDemo
		AS
		(SELECT
				td.[Id]
			   ,td.[FieldString]
			   ,td.[FieldDateTime]
			   ,td.[FieldDropdownId]
			   ,td1.[FieldString] AS FieldDropdownName
			   ,td.[FieldNumber]
			   ,td.[FieldTextArea]
			   ,td.[FieldImageSingle]
			   ,td.[FieldCheckBox]
			   ,td.[FieldSwitch]
			   ,td.[CreatedDate]
			FROM TestDemo AS td
			LEFT JOIN TestDemo AS td1
				ON td1.Id = td.FieldDropdownId
				AND td1.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR td.[FieldString] LIKE CONCAT('%', @SearchText, '%')
			)
			AND td.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultTestDemo)
		SELECT
			*
		FROM TempResultTestDemo r
			,TempCount
		ORDER BY
		--ascend
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldString' THEN r.[FieldString]
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldDateTime' THEN r.FieldDateTime
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldDropdownName' THEN r.FieldDropdownName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldNumber' THEN r.FieldNumber
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldTextArea' THEN r.FieldTextArea
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldImageSingle' THEN r.FieldImageSingle
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldCheckBox' THEN r.FieldCheckBox
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fieldSwitch' THEN r.FieldSwitch
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldString' THEN r.FieldString
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldDateTime' THEN r.FieldDateTime
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldDropdownName' THEN r.FieldDropdownName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldNumber' THEN r.FieldNumber
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldTextArea' THEN r.FieldTextArea
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldImageSingle' THEN r.FieldImageSingle
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldCheckBox' THEN r.FieldCheckBox
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fieldSwitch' THEN r.FieldSwitch
		END DESC,
		--default
		CASE
			WHEN ISNULL(@SortOrder, '') = '' THEN r.CreatedDate
		END DESC
		OFFSET (@PageSize * (@Page - 1)) ROWS
		FETCH NEXT @PageSize
		ROWS ONLY
		OPTION (RECOMPILE);

	END TRY
	BEGIN
	CATCH
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

/****** uspUnitMeasure_GetAll ******/
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

/****** uspUser_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUser_GetAll] (@FullName NVARCHAR(MAX)
, @UserName NVARCHAR(MAX)
, @UserGroupId NVARCHAR(MAX)
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

/****** uspUserClaim_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUserClaim_GetAll] (@UserId UNIQUEIDENTIFIER
, @UserGroupId UNIQUEIDENTIFIER
, @Page INT
, @PageSize INT)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		;
		WITH TempResultUserClaim
		AS
		(SELECT
				uc.[Id]
			   ,uc.[UserId]
			   ,u.UserName AS UserName
			   ,uc.[UserGroupId]
			   ,ug.[Name] AS UserGroupName
			   ,uc.[CreatedDate]
			FROM UserClaim AS uc
			LEFT JOIN [User] AS u
				ON u.Id = uc.[UserId]
				AND u.IsDeleted = 0
			LEFT JOIN UserGroup AS ug
				ON ug.Id = uc.[UserGroupId]
				AND ug.IsDeleted = 0
			WHERE (
			@UserId IS NULL
			OR uc.[UserId] = @UserId
			)
			AND (
			@UserGroupId IS NULL
			OR uc.[UserGroupId] = @UserGroupId
			)
			AND uc.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultUserClaim)
		SELECT
			*
		FROM TempResultUserClaim r
			,TempCount
		ORDER BY r.CreatedDate DESC OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
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

/****** uspUserGroup_GetAll ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUserGroup_GetAll] (@SearchText NVARCHAR(MAX)
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
		WITH TempResultUserGroup
		AS
		(SELECT
				Id
			   ,[Name]
			   ,IsDefault
			   ,CreatedDate
			FROM UserGroup
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR [Name] LIKE CONCAT('%', @SearchText, '%')
			)
			AND IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultUserGroup)
		SELECT
			*
		FROM TempResultUserGroup r
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

/****** uspUserRole_GetByUserGroupId ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUserRole_GetByUserGroupId] @ListUserGroupId NVARCHAR(MAX)
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT
		   ,@intErrorLine INT;

	BEGIN TRY

		SELECT
			ur.Id
		   ,om.[Name] AS ObjectMenuName
		   ,om.ShortName
		   ,om.ParentId
		   ,om.MetaLink
		   ,om.IconName
		   ,om.LevelShow
		   ,om.OrderShow
		   ,om.DataTarget
		   ,om.AriaControls
		   ,ur.ObjectMenuId
		   ,ur.AllowAccess
		   ,ur.AllowAdd
		   ,ur.AllowEdit
		   ,ur.AllowDelete
		   ,ur.AllowPrint
		   ,ur.AllowExport
		   ,ur.AllowImport
		FROM UserRole AS ur
		LEFT JOIN ObjectMenu AS om
			ON om.Id = ur.ObjectMenuId
				AND om.IsDeleted = 0
		WHERE ur.UserGroupId IN (SELECT
				value
			FROM STRING_SPLIT(@ListUserGroupId, ','))
		AND om.IsActive = 1
		AND ur.IsDeleted = 0

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

