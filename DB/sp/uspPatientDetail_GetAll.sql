-- Extracted from DB dev AppDelicare (read-only) on 2026-11-14 by T00
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[uspPatientDetail_GetAll] (@SearchText NVARCHAR(MAX)
, @PhoneNumber NVARCHAR(MAX)
, @CreatedDate DATETIME
, @PersonId UNIQUEIDENTIFIER
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
		WITH TempResultPatient
		AS
		(SELECT
				pd.[Id]
			   ,p.[FullName]
			   ,p.PhoneNumber
			   ,p.DateOfBirth
			   ,p.[Sex]
			   ,ap.NameVN AS AdministrativeProvinceName
			   ,ad.NameVN AS AdministrativeDistrictName
			   ,aw.NameVN AS AdministrativeWardName
			   ,p.[Address]
			   ,p1.[FullName] AS PersonName
			   ,p.[CreatedDate]
			   ,pd.[Result]
			FROM PatientDetail AS pd
			LEFT JOIN Patient AS p
				ON p.Id = pd.PatientId
				AND p.IsDeleted = 0
			LEFT JOIN AdministrativeProvince AS ap
				ON ap.Id = p.AdministrativeProvinceId
				AND ap.IsDeleted = 0
			LEFT JOIN AdministrativeDistrict AS ad
				ON ad.Id = p.AdministrativeDistrictId
				AND ad.IsDeleted = 0
			LEFT JOIN AdministrativeWard AS aw
				ON aw.Id = p.AdministrativeWardId
				AND aw.IsDeleted = 0
			LEFT JOIN [User] AS u
				ON u.Id = p.CreatedBy
				AND u.IsDeleted = 0
			LEFT JOIN Person AS p1
				ON p1.Id = u.PersonId
				AND u.IsDeleted = 0
			WHERE (
			ISNULL(@SearchText, '') = ''
			OR p.FullName LIKE CONCAT('%', @SearchText, '%')
			)
			AND (
			ISNULL(@PhoneNumber, '') = ''
			OR p.PhoneNumber LIKE CONCAT('%', @PhoneNumber, '%')
			)
			AND (
			ISNULL(@CreatedDate, '') = ''
			OR CONVERT(VARCHAR(10), p.CreatedDate, 103) = CONVERT(VARCHAR(10), @CreatedDate, 103)
			)
			AND pd.PersonId = @PersonId
			AND pd.IsDeleted = 0),
		TempCount (TotalRow)
		AS
		(SELECT
				COUNT(*)
			FROM TempResultPatient)
		SELECT
			*
		FROM TempResultPatient r
			,TempCount
		ORDER BY
		--ascend 
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'fullName' THEN r.FullName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'phoneNumber' THEN r.PhoneNumber
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'dateOfBirth' THEN r.DateOfBirth
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'sex' THEN r.Sex
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END ASC,
		CASE
			WHEN @SortOrder = 'ascend' AND
				@SortField = 'result' THEN r.Result
		END ASC,
		--descend
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'fullName' THEN r.FullName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'phoneNumber' THEN r.PhoneNumber
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'dateOfBirth' THEN r.DateOfBirth
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'sex' THEN r.Sex
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'administrativeProvinceName' THEN r.AdministrativeProvinceName
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'createdDate' THEN r.CreatedDate
		END DESC,
		CASE
			WHEN @SortOrder = 'descend' AND
				@SortField = 'result' THEN r.Result
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
