-- T16 (spec 7.6 inventory-transactions): sổ cái kho có lọc khoảng ngày, chỉ đọc. Viết tay (không do CodeGen sinh).
-- Trả cùng cột với uspInventoryTransaction_GetAll (InventoryTransactionViewModel) + TotalRow. Mặc định mới nhất trước.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspInventoryTransaction_GetLedger] (
	@SearchText NVARCHAR(200) = NULL
	, @WarehouseId UNIQUEIDENTIFIER = NULL
	, @IngredientId UNIQUEIDENTIFIER = NULL
	, @MaterialLotId UNIQUEIDENTIFIER = NULL
	, @TransactionType NVARCHAR(50) = NULL
	, @ReferenceType NVARCHAR(100) = NULL
	, @ReferenceId UNIQUEIDENTIFIER = NULL
	, @FromDate DATETIME = NULL
	, @ToDate DATETIME = NULL
	, @Page INT = 1
	, @PageSize INT = 20
	, @SortField NVARCHAR(100) = NULL
	, @SortOrder NVARCHAR(20) = NULL)
AS
BEGIN
	SET NOCOUNT ON;
	IF @Page IS NULL OR @Page < 1 SET @Page = 1;
	IF @PageSize IS NULL OR @PageSize < 1 SET @PageSize = 20;
	IF @PageSize > 1000 SET @PageSize = 1000;
	DECLARE @From DATETIME = CASE WHEN @FromDate IS NULL THEN NULL ELSE CAST(CAST(@FromDate AS DATE) AS DATETIME) END;
	DECLARE @ToExclusive DATETIME = CASE WHEN @ToDate IS NULL THEN NULL ELSE DATEADD(DAY, 1, CAST(CAST(@ToDate AS DATE) AS DATETIME)) END;

	;WITH TempResult AS
	(
		SELECT
			t.[Id]
			, t.[TransactionType]
			, t.[WarehouseId]
			, w.[Name] AS [WarehouseName]
			, t.[StorageLocationId]
			, sl.[Name] AS [StorageLocationName]
			, t.[IngredientId]
			, i.[Name] AS [IngredientName]
			, t.[ReferenceType]
			, t.[ReferenceId]
			, t.[Quantity]
			, t.[UnitCost]
			, t.[TransactionDate]
			, t.[Note]
			, t.[MaterialLotId]
			, COALESCE(l.[LotNumber], l.[LotNo]) AS [MaterialLotName]
			, t.[TransactionNumber]
			, t.[QuantityIn]
			, t.[QuantityOut]
			, t.[UnitId]
			, u.[Name] AS [UnitName]
			, t.[Reason]
			, t.[CreatedDate]
		FROM [dbo].[InventoryTransaction] AS t
		LEFT JOIN [dbo].[Warehouse] AS w ON w.[Id] = t.[WarehouseId]
		LEFT JOIN [dbo].[StorageLocation] AS sl ON sl.[Id] = t.[StorageLocationId]
		LEFT JOIN [dbo].[Ingredient] AS i ON i.[Id] = t.[IngredientId]
		LEFT JOIN [dbo].[MaterialLot] AS l ON l.[Id] = t.[MaterialLotId]
		LEFT JOIN [dbo].[UnitMeasure] AS u ON u.[Id] = t.[UnitId]
		WHERE t.[IsDeleted] = 0
			AND (ISNULL(@SearchText, '') = ''
				OR t.[TransactionNumber] LIKE CONCAT('%', @SearchText, '%')
				OR i.[Name] LIKE CONCAT('%', @SearchText, '%')
				OR i.[Code] LIKE CONCAT('%', @SearchText, '%')
				OR l.[LotNumber] LIKE CONCAT('%', @SearchText, '%'))
			AND (@WarehouseId IS NULL OR t.[WarehouseId] = @WarehouseId)
			AND (@IngredientId IS NULL OR t.[IngredientId] = @IngredientId)
			AND (@MaterialLotId IS NULL OR t.[MaterialLotId] = @MaterialLotId)
			AND (ISNULL(@TransactionType, '') = '' OR t.[TransactionType] = @TransactionType)
			AND (ISNULL(@ReferenceType, '') = '' OR t.[ReferenceType] = @ReferenceType)
			AND (@ReferenceId IS NULL OR t.[ReferenceId] = @ReferenceId)
			AND (@From IS NULL OR t.[TransactionDate] >= @From)
			AND (@ToExclusive IS NULL OR t.[TransactionDate] < @ToExclusive)
	),
	TempCount (TotalRow) AS (SELECT COUNT(*) FROM TempResult)
	SELECT r.*, c.TotalRow
	FROM TempResult AS r CROSS JOIN TempCount AS c
	ORDER BY
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'transactionDate' THEN r.[TransactionDate] END ASC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'transactionNumber' THEN r.[TransactionNumber] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'transactionNumber' THEN r.[TransactionNumber] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'ingredientName' THEN r.[IngredientName] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'ingredientName' THEN r.[IngredientName] END DESC,
		CASE WHEN @SortOrder = 'ascend' AND @SortField = 'transactionType' THEN r.[TransactionType] END ASC,
		CASE WHEN @SortOrder = 'descend' AND @SortField = 'transactionType' THEN r.[TransactionType] END DESC,
		r.[TransactionDate] DESC,
		r.[CreatedDate] DESC
	OFFSET (@PageSize * (@Page - 1)) ROWS FETCH NEXT @PageSize ROWS ONLY
	OPTION (RECOMPILE);
END
GO
