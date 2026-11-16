-- T45R: danh sách xung đột chuẩn hóa dữ liệu (màn adm-merge-conflict, spec 3.6). Bảng không có IsDeleted; Id bigint.
-- Viết tay theo mẫu usp<Entity>_GetAll (CodeGen không sinh cho bảng Id bigint).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspCoreDataMergeConflict_GetAll] (
	@SearchText NVARCHAR(MAX) = NULL
	, @IsResolved BIT = NULL
	, @EntityName NVARCHAR(128) = NULL
	, @MigrationCode NVARCHAR(100) = NULL
	, @Page INT = 1
	, @PageSize INT = 10
	, @SortField NVARCHAR(100) = NULL
	, @SortOrder NVARCHAR(20) = NULL)
AS
BEGIN
	SET NOCOUNT ON;
	IF @Page IS NULL OR @Page < 1 SET @Page = 1;
	IF @PageSize IS NULL OR @PageSize < 1 SET @PageSize = 10;
	IF @PageSize > 1000 SET @PageSize = 1000;

	;WITH TempResult AS
	(
		SELECT
			t.[Id]
			, t.[MigrationCode]
			, t.[EntityName]
			, t.[RecordId]
			, t.[FieldName]
			, t.[CanonicalValue]
			, t.[LegacyValue]
			, t.[DetectedAt]
			, t.[ResolvedAt]
			, t.[ResolutionNote]
		FROM [dbo].[CoreDataMergeConflict] AS t
		WHERE (ISNULL(@SearchText, '') = '' OR t.[EntityName] LIKE CONCAT('%', @SearchText, '%') OR t.[FieldName] LIKE CONCAT('%', @SearchText, '%')
				OR t.[LegacyValue] LIKE CONCAT('%', @SearchText, '%') OR t.[CanonicalValue] LIKE CONCAT('%', @SearchText, '%'))
			AND (@IsResolved IS NULL OR (@IsResolved = 1 AND t.[ResolvedAt] IS NOT NULL) OR (@IsResolved = 0 AND t.[ResolvedAt] IS NULL))
			AND (ISNULL(@EntityName, '') = '' OR t.[EntityName] = @EntityName)
			AND (ISNULL(@MigrationCode, '') = '' OR t.[MigrationCode] = @MigrationCode)
	),
	TempCount (TotalRow) AS (SELECT COUNT(*) FROM TempResult)
	SELECT r.*, c.TotalRow
	FROM TempResult AS r CROSS JOIN TempCount AS c
	ORDER BY
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'entityName' THEN r.[EntityName] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'entityName' THEN r.[EntityName] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'detectedAt' THEN r.[DetectedAt] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'detectedAt' THEN r.[DetectedAt] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'resolvedAt' THEN r.[ResolvedAt] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'resolvedAt' THEN r.[ResolvedAt] END DESC,
		r.[DetectedAt] DESC
	OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
	OPTION (RECOMPILE);
END
GO
