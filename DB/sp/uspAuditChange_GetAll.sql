-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
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
