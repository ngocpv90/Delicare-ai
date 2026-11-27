-- RETAIL-1: danh sách sản phẩm bán lẻ (màn #/sal-retail-product) – lọc tìm mã / tên, nhóm, trạng thái; sắp theo SortOrder.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspRetailProduct_GetAll] (
	@SearchText NVARCHAR(MAX) = NULL
	, @CategoryCode NVARCHAR(50) = NULL
	, @Status NVARCHAR(30) = NULL
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
			, t.[Code]
			, t.[Name]
			, t.[CategoryCode]
			, t.[MealTypes]
			, t.[Unit]
			, t.[Price]
			, t.[Description]
			, t.[Tags]
			, t.[Icon]
			, t.[ImageUrl]
			, t.[SortOrder]
			, t.[Status]
			, t.[CreatedDate]
		FROM [dbo].[RetailProduct] AS t
		
		WHERE (ISNULL(@SearchText, '') = '' OR t.[Code] LIKE CONCAT('%', @SearchText, '%') OR t.[Name] LIKE CONCAT('%', @SearchText, '%') )
			AND (ISNULL(@CategoryCode, '') = '' OR t.[CategoryCode] = @CategoryCode)
			AND (ISNULL(@Status, '') = '' OR t.[Status] = @Status)
			AND t.IsDeleted = 0
	),
	TempCount (TotalRow) AS (SELECT COUNT(*) FROM TempResult)
	SELECT r.*, c.TotalRow
	FROM TempResult AS r CROSS JOIN TempCount AS c
	ORDER BY
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'categoryCode' THEN r.[CategoryCode] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'categoryCode' THEN r.[CategoryCode] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'mealTypes' THEN r.[MealTypes] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'mealTypes' THEN r.[MealTypes] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'unit' THEN r.[Unit] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'unit' THEN r.[Unit] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'price' THEN r.[Price] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'price' THEN r.[Price] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'description' THEN r.[Description] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'description' THEN r.[Description] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'tags' THEN r.[Tags] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'tags' THEN r.[Tags] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'icon' THEN r.[Icon] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'icon' THEN r.[Icon] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'imageUrl' THEN r.[ImageUrl] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'imageUrl' THEN r.[ImageUrl] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'sortOrder' THEN r.[SortOrder] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'sortOrder' THEN r.[SortOrder] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'createdDate' THEN r.[CreatedDate] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'createdDate' THEN r.[CreatedDate] END DESC,
		r.[SortOrder] ASC, r.[Name] ASC
	OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
	OPTION (RECOMPILE);
END
GO
