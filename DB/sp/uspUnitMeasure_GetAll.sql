-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00.
-- FE-2 follow-up (2026-11-16, cùng T14): trả đủ cột của UnitMeasureViewModel (Code, Symbol, UomType, UnitType, DecimalPrecision, Status)
-- và lọc @Status (tham số mới có mặc định NULL – gọi kiểu cũ vẫn chạy). Tìm theo Code / Name / Symbol. Không đổi bảng.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspUnitMeasure_GetAll] (
	@SearchText NVARCHAR(MAX) = NULL
	, @Status NVARCHAR(50) = NULL
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

	;WITH TempResultUnitMeasure AS
	(
		SELECT
			um.[Id]
			, um.[Code]
			, um.[Name]
			, um.[Symbol]
			, um.[UomType]
			, um.[UnitType]
			, um.[DecimalPrecision]
			, um.[Status]
			, um.[CreatedDate]
		FROM [dbo].[UnitMeasure] AS um
		WHERE (ISNULL(@SearchText, '') = ''
				OR um.[Code] LIKE CONCAT('%', @SearchText, '%')
				OR um.[Name] LIKE CONCAT('%', @SearchText, '%')
				OR um.[Symbol] LIKE CONCAT('%', @SearchText, '%'))
			AND (ISNULL(@Status, '') = '' OR um.[Status] = @Status)
			AND um.IsDeleted = 0
	),
	TempCount (TotalRow) AS (SELECT COUNT(*) FROM TempResultUnitMeasure)
	SELECT r.*, t.TotalRow
	FROM TempResultUnitMeasure AS r CROSS JOIN TempCount AS t
	ORDER BY
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'uomType' THEN r.[UomType] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'uomType' THEN r.[UomType] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'createdDate' THEN r.[CreatedDate] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'createdDate' THEN r.[CreatedDate] END DESC,
		r.[CreatedDate] DESC
	OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
	OPTION (RECOMPILE);
END
GO
