-- Tables + list procedures for Region (Vùng) and Area (Khu vực).
-- Entities exist in code (Model/Entity/Region.cs, Area.cs) and the web has screens for them,
-- but the tables were never created in the database.
-- Re-runnable: tables are only created if missing; procedures use CREATE OR ALTER.
USE [Delicare]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'[dbo].[Region]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[Region](
		[Id] [uniqueidentifier] NOT NULL,
		[Code] [nvarchar](max) NULL,
		[CodeName] [nvarchar](max) NULL,
		[NameVN] [nvarchar](max) NULL,
		[NameEN] [nvarchar](max) NULL,
		[FullNameVN] [nvarchar](max) NULL,
		[FullNameEN] [nvarchar](max) NULL,
		[IsDefault] [bit] NOT NULL,
		[CreatedDate] [datetime] NOT NULL,
		[CreatedBy] [uniqueidentifier] NULL,
		[ModifiedDate] [datetime] NULL,
		[ModifiedBy] [uniqueidentifier] NULL,
		[DeletedDate] [datetime] NULL,
		[DeleteddBy] [uniqueidentifier] NULL,
		[IsDeleted] [bit] NOT NULL,
	 CONSTRAINT [PK_Region] PRIMARY KEY CLUSTERED ([Id] ASC)
	);
END
GO

IF OBJECT_ID(N'[dbo].[Area]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[Area](
		[Id] [uniqueidentifier] NOT NULL,
		[Code] [nvarchar](max) NULL,
		[CodeName] [nvarchar](max) NULL,
		[NameVN] [nvarchar](max) NULL,
		[NameEN] [nvarchar](max) NULL,
		[FullNameVN] [nvarchar](max) NULL,
		[FullNameEN] [nvarchar](max) NULL,
		[RegionId] [uniqueidentifier] NULL,
		[IsDefault] [bit] NOT NULL,
		[CreatedDate] [datetime] NOT NULL,
		[CreatedBy] [uniqueidentifier] NULL,
		[ModifiedDate] [datetime] NULL,
		[ModifiedBy] [uniqueidentifier] NULL,
		[DeletedDate] [datetime] NULL,
		[DeleteddBy] [uniqueidentifier] NULL,
		[IsDeleted] [bit] NOT NULL,
	 CONSTRAINT [PK_Area] PRIMARY KEY CLUSTERED ([Id] ASC)
	);
	CREATE NONCLUSTERED INDEX [IX_Area_RegionId] ON [dbo].[Area] ([RegionId]);
END
GO

/****** uspRegion_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspRegion_GetAll] (@SearchText NVARCHAR(MAX)
, @Page INT
, @PageSize INT
, @SortField NVARCHAR(MAX)
, @SortOrder NVARCHAR(MAX))
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT;

	BEGIN TRY

		;
		WITH TempResultRegion
		AS
		(SELECT
				r.[Id]
			   ,r.[Code]
			   ,r.[CodeName]
			   ,r.[NameVN]
			   ,r.[NameEN]
			   ,r.[FullNameVN]
			   ,r.[FullNameEN]
			   ,r.[IsDefault]
			   ,r.[CreatedDate]
			FROM Region AS r
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR r.[Code] LIKE CONCAT('%', @SearchText, '%')
			OR r.[CodeName] LIKE CONCAT('%', @SearchText, '%')
			OR r.[NameVN] LIKE CONCAT('%', @SearchText, '%')
			OR r.[NameEN] LIKE CONCAT('%', @SearchText, '%')
			OR r.[FullNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR r.[FullNameEN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND r.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultRegion)
		SELECT
			*
		FROM TempResultRegion t
			,TempCount
		ORDER BY
		--ascend
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN t.[Code] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'codeName' THEN t.[CodeName] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'nameVN' THEN t.[NameVN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'nameEN' THEN t.[NameEN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'fullNameVN' THEN t.[FullNameVN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'fullNameEN' THEN t.[FullNameEN] END ASC,
		--descend
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN t.[Code] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'codeName' THEN t.[CodeName] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'nameVN' THEN t.[NameVN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'nameEN' THEN t.[NameEN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'fullNameVN' THEN t.[FullNameVN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'fullNameEN' THEN t.[FullNameEN] END DESC,
		--default
		t.CreatedDate DESC
		OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
		OPTION (RECOMPILE);

	END TRY
	BEGIN CATCH
		SELECT
			@strErrorMessage = ERROR_MESSAGE() + ' Line:' + CONVERT(VARCHAR(5), ERROR_LINE())
		   ,@intErrorSeverity = ERROR_SEVERITY()
		   ,@intErrorState = ERROR_STATE();
		RAISERROR (@strErrorMessage, @intErrorSeverity, @intErrorState);
	END CATCH;
END
GO

/****** uspArea_GetAll ******/
CREATE OR ALTER PROCEDURE [dbo].[uspArea_GetAll] (@SearchText NVARCHAR(MAX)
, @RegionId UNIQUEIDENTIFIER = NULL
, @Page INT
, @PageSize INT
, @SortField NVARCHAR(MAX)
, @SortOrder NVARCHAR(MAX))
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @strErrorMessage NVARCHAR(4000)
		   ,@intErrorSeverity INT
		   ,@intErrorState INT;

	BEGIN TRY

		;
		WITH TempResultArea
		AS
		(SELECT
				a.[Id]
			   ,a.[Code]
			   ,a.[CodeName]
			   ,a.[NameVN]
			   ,a.[NameEN]
			   ,a.[FullNameVN]
			   ,a.[FullNameEN]
			   ,a.[RegionId]
			   ,r.[NameVN] AS RegionName
			   ,a.[IsDefault]
			   ,a.[CreatedDate]
			FROM Area AS a
			LEFT JOIN Region AS r
				ON r.Id = a.[RegionId]
				AND r.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR a.[Code] LIKE CONCAT('%', @SearchText, '%')
			OR a.[CodeName] LIKE CONCAT('%', @SearchText, '%')
			OR a.[NameVN] LIKE CONCAT('%', @SearchText, '%')
			OR a.[NameEN] LIKE CONCAT('%', @SearchText, '%')
			OR a.[FullNameVN] LIKE CONCAT('%', @SearchText, '%')
			OR a.[FullNameEN] LIKE CONCAT('%', @SearchText, '%')
			OR r.[NameVN] LIKE CONCAT('%', @SearchText, '%')
			)
			AND (@RegionId IS NULL OR a.[RegionId] = @RegionId)
			AND a.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultArea)
		SELECT
			*
		FROM TempResultArea t
			,TempCount
		ORDER BY
		--ascend
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN t.[Code] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'codeName' THEN t.[CodeName] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'nameVN' THEN t.[NameVN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'nameEN' THEN t.[NameEN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'fullNameVN' THEN t.[FullNameVN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'fullNameEN' THEN t.[FullNameEN] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'regionName' THEN t.[RegionName] END ASC,
		--descend
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN t.[Code] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'codeName' THEN t.[CodeName] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'nameVN' THEN t.[NameVN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'nameEN' THEN t.[NameEN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'fullNameVN' THEN t.[FullNameVN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'fullNameEN' THEN t.[FullNameEN] END DESC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'regionName' THEN t.[RegionName] END DESC,
		--default
		t.CreatedDate DESC
		OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
		OPTION (RECOMPILE);

	END TRY
	BEGIN CATCH
		SELECT
			@strErrorMessage = ERROR_MESSAGE() + ' Line:' + CONVERT(VARCHAR(5), ERROR_LINE())
		   ,@intErrorSeverity = ERROR_SEVERITY()
		   ,@intErrorState = ERROR_STATE();
		RAISERROR (@strErrorMessage, @intErrorSeverity, @intErrorState);
	END CATCH;
END
GO
