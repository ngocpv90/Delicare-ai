-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00.
-- FE-2 follow-up (2026-11-16, cùng T14): trả đủ cột của SupplierViewModel (Code, Type, MST, liên hệ, Status, CompanyName…) và lọc @CompanyId / @Status
-- (tham số mới có mặc định NULL – gọi kiểu cũ vẫn chạy). Tìm theo Code / Name / TaxCode / Phone. Không đổi bảng.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspSupplier_GetAll] (
	@SearchText NVARCHAR(MAX) = NULL
	, @CompanyId UNIQUEIDENTIFIER = NULL
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

	;WITH TempResultSupplier AS
	(
		SELECT
			s.[Id]
			, s.[CompanyId]
			, c.[Name] AS [CompanyName]
			, s.[Code]
			, s.[Name]
			, s.[SupplierType]
			, s.[TaxCode]
			, s.[Address]
			, s.[Phone]
			, s.[Email]
			, s.[ContactName]
			, s.[ContactPhone]
			, s.[Status]
			, s.[Note]
			, s.[CreatedDate]
		FROM [dbo].[Supplier] AS s
		LEFT JOIN [dbo].[Company] AS c ON c.[Id] = s.[CompanyId]
		WHERE (ISNULL(@SearchText, '') = ''
				OR s.[Code] LIKE CONCAT('%', @SearchText, '%')
				OR s.[Name] LIKE CONCAT('%', @SearchText, '%')
				OR s.[TaxCode] LIKE CONCAT('%', @SearchText, '%')
				OR s.[Phone] LIKE CONCAT('%', @SearchText, '%'))
			AND (@CompanyId IS NULL OR s.[CompanyId] = @CompanyId)
			AND (ISNULL(@Status, '') = '' OR s.[Status] = @Status)
			AND s.IsDeleted = 0
	),
	TempCount (TotalRow) AS (SELECT COUNT(*) FROM TempResultSupplier)
	SELECT r.*, t.TotalRow
	FROM TempResultSupplier AS r CROSS JOIN TempCount AS t
	ORDER BY
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'code' THEN r.[Code] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'code' THEN r.[Code] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'name' THEN r.[Name] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'name' THEN r.[Name] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'supplierType' THEN r.[SupplierType] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'supplierType' THEN r.[SupplierType] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'taxCode' THEN r.[TaxCode] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'taxCode' THEN r.[TaxCode] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'status' THEN r.[Status] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'status' THEN r.[Status] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'note' THEN r.[Note] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'note' THEN r.[Note] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'createdDate' THEN r.[CreatedDate] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'createdDate' THEN r.[CreatedDate] END DESC,
		r.[CreatedDate] DESC
	OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
	OPTION (RECOMPILE);
END
GO
