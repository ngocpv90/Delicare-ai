-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
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
