-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
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
