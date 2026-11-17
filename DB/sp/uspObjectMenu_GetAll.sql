-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
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
	-- Rà soát BE: chặn PageSize / Page bất thường (như các SP GetAll khác) – PageSize=1000000 từng trả cả bảng.
	IF @Page IS NULL OR @Page < 1 SET @Page = 1;
	IF @PageSize IS NULL OR @PageSize < 1 SET @PageSize = 10;
	IF @PageSize > 1000 SET @PageSize = 1000;
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
