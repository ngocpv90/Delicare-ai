/*
================================================================================
DELICARE - DATABASE EVOLUTION SCRIPT (CORRECTED FROM REAL APPDELICARE SCHEMA)
Database: AppDelicare | SQL Server 2019+ | Physical schema: dbo ONLY

SOURCE OF TRUTH:
- Actual AppDelicare database structure supplied by the user: 91 existing tables.
- The separate 42-object architecture file is DESIGN REFERENCE ONLY.
- Reference objects are not treated as existing database tables.
- This script does not create source-schema objects or ArchitectureObjectMapping.

PHASE 1 - Normalize existing core:
- Merge Supplier/Suppliers into dbo.Supplier.
- Merge UnitMeasure/UnitsOfMeasure into dbo.UnitMeasure.
- Merge ActualCost/ActualCosts into dbo.ActualCost.
- Preserve plural tables as legacy archives and create temporary dbo synonyms.

PHASE 2 - Extend actual AppDelicare operations:
- Reuse existing Recipe, ProductionOrder, ProductionBatche, Shift, Employee,
  QCInspection, CleaningBatche, DeliveryRoute, DeliveryOrder, ContainerReturn.
- Add missing production line/workstation, shift assignment/attendance,
  food safety checklist/temperature, delivery stop/events, packaging ledger,
  meal waste, supplier issue, customer complaint, cleaning checklist,
  menu change request.
- Recipe versioning is added to dbo.Recipe; no new RecipeVersion entity.

PHASE 3 - Add workflow and channel integration:
- ApprovalRequest/Step/Decision/Item; LeaveRequest/ShiftSwapRequest.
- IntegrationChannel/BotConfiguration/UserExternalIdentity/InboundMessage/
  OutboundMessage/IntegrationLog.
- Secrets are references only; do not store plaintext tokens.

PHASE 4 - Add analytics foundation:
- KPI metric definitions, daily/monthly snapshots, refresh log.
- KPI formulas and source calculations are not invented by this migration.

DEPRECATION:
- If the earlier script created ArchitectureObjectMapping or RecipeVersion,
  archive them as *_Legacy_ReferenceOnly; do not delete their data.
- Patient, PatientDetail, MedicalExamination, MedicineWarehouse and TestDemo
  are candidates for later deprecation; they are NOT dropped automatically.
- Misspelled existing table names are retained for application compatibility.

SAFETY:
- Full backup and verified restore are mandatory; test on UAT first.
- Stop application writes during Phase 1.
- Phase 1 stops if existing database object or SQL module dependencies block consolidation.
- No DROP TABLE or DELETE is performed.
- New FK relationships are not forced onto unknown existing data; validate
  and add application-specific constraints after UAT review.
- This migration does not update .NET entities, EF mappings, APIs or Angular.
================================================================================
*/
SET NOCOUNT ON;
GO
/*
================================================================================
DELICARE DATABASE CORE V2 - ONE SCHEMA (dbo) / ARCHITECTURE MATCHING
Source architecture:
  - DELICARE: 91 dbo tables, SQL Server, existing application compatibility kept.
  - Reference: 42 tables organized by source schemas dm / vh / ht / bc.
Design decision:
  - DELICARE remains the source of truth and canonical data model.
  - dbo is the ONLY physical schema in DELICARE; no new schemas are created.
  - Consolidate duplicate core tables by Id.
  - Keep legacy-name synonyms in dbo only, as a temporary SQL compatibility bridge.
  - Treat the 42-object reference file as design input only.
  - Reference schema names are stored as descriptive source/module labels only.
  - Do NOT move existing tables or create domain synonyms.

TARGET DATABASE: AppDelicare
SQL Server: 2019+
RUN: First on a restored UAT database. One deployment at a time.

SAFETY:
  1. Take and test a full backup.
  2. This script stops if actual FK constraints or SQL module dependencies point
     to plural legacy tables. Resolve those dependencies first.
  3. Same-Id conflicts are logged; populated canonical values are not overwritten.
  4. Different IDs are not auto-merged by Code/Name/TaxCode.
  5. Legacy tables are renamed and retained; synonyms preserve old SQL object names.
  6. Synonyms are a transition bridge, not physical tables and cannot be FK targets.
  7. No DeleteddBy columns are dropped in this migration.
================================================================================
*/
USE [AppDelicare];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF DB_NAME() <> N'AppDelicare'
    THROW 51000, 'Run this migration only in AppDelicare.', 1;
GO

/* 01. SCHEMA POLICY
   DELICARE uses dbo as its ONLY physical schema.
   The reference architecture is a design input only; its schema names and
   object names are not created in AppDelicare. No schema moves are performed.
*/
/* 02. PRE-FLIGHT: verify canonical and legacy objects */
IF OBJECT_ID(N'dbo.Supplier', N'U') IS NULL
    THROW 51001, 'Required table dbo.Supplier does not exist.', 1;
IF OBJECT_ID(N'dbo.UnitMeasure', N'U') IS NULL
    THROW 51002, 'Required table dbo.UnitMeasure does not exist.', 1;
IF OBJECT_ID(N'dbo.ActualCost', N'U') IS NULL
    THROW 51003, 'Required table dbo.ActualCost does not exist.', 1;

SELECT
    t.name AS TableName,
    SUM(p.rows) AS ApproxRowCount
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.schema_id = SCHEMA_ID(N'dbo')
  AND t.name IN
  (
    N'ActualCost', N'ActualCosts'
  )
GROUP BY t.name
ORDER BY t.name;
GO

/* Stop rather than silently break actual FK constraints. */
--IF EXISTS
--(
--GO

/* Stop if stored procedures/views/functions have recorded dependencies on
   plural tables. Review sys.sql_expression_dependencies before migration. */
/*IF EXISTS
(
    SELECT 1
    FROM sys.sql_expression_dependencies d
    WHERE d.referenced_id IN
    (
        --OBJECT_ID(N'dbo.Suppliers', N'U'),
        --OBJECT_ID(N'dbo.UnitsOfMeasure', N'U'),
        OBJECT_ID(N'dbo.ActualCosts', N'U')
    )
      AND d.referencing_id IS NOT NULL
)
    THROW 51005, 'SQL modules depend on plural legacy tables. Review/update module dependencies before running this script.', 1;
GO */

/* 03. CONFLICT REGISTER */
IF OBJECT_ID(N'dbo.CoreDataMergeConflict', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoreDataMergeConflict
    (
        Id              bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_CoreDataMergeConflict PRIMARY KEY,
        MigrationCode   nvarchar(50) NOT NULL,
        EntityName      sysname NOT NULL,
        RecordId        uniqueidentifier NULL,
        FieldName       sysname NOT NULL,
        CanonicalValue  nvarchar(max) NULL,
        LegacyValue     nvarchar(max) NULL,
        DetectedAt      datetime2(7) NOT NULL
            CONSTRAINT DF_CoreDataMergeConflict_DetectedAt DEFAULT SYSUTCDATETIME(),
        ResolvedAt      datetime2(7) NULL,
        ResolutionNote  nvarchar(1000) NULL
    );
END;


BEGIN TRY
    BEGIN TRANSACTION;

    /* =========================================================================
       04A. SUPPLIER + SUPPLIERS -> dbo.Supplier
       Singular table is canonical; richer plural columns are added.
       ========================================================================= */
/*    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.Suppliers_Legacy_20261113', N'U') IS NOT NULL
            THROW 51006, 'Suppliers legacy archive already exists. Inspect before retrying.', 1;

        /* Add fields that exist in Suppliers but not in Supplier. */
        IF COL_LENGTH(N'dbo.Supplier', N'Code') IS NULL
            ALTER TABLE dbo.Supplier ADD Code nvarchar(255) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'SupplierType') IS NULL
            ALTER TABLE dbo.Supplier ADD SupplierType nvarchar(100) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'TaxCode') IS NULL
            ALTER TABLE dbo.Supplier ADD TaxCode nvarchar(100) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'Address') IS NULL
            ALTER TABLE dbo.Supplier ADD Address nvarchar(max) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'Phone') IS NULL
            ALTER TABLE dbo.Supplier ADD Phone nvarchar(100) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'Email') IS NULL
            ALTER TABLE dbo.Supplier ADD Email nvarchar(255) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'Status') IS NULL
            ALTER TABLE dbo.Supplier ADD Status nvarchar(50) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'CompanyId') IS NULL
            ALTER TABLE dbo.Supplier ADD CompanyId uniqueidentifier NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'ContactName') IS NULL
            ALTER TABLE dbo.Supplier ADD ContactName nvarchar(max) NULL;
        IF COL_LENGTH(N'dbo.Supplier', N'DeletedBy') IS NULL
            ALTER TABLE dbo.Supplier ADD DeletedBy uniqueidentifier NULL;

        /* Same Id but different populated values -> review queue. */
        INSERT dbo.CoreDataMergeConflict
            (MigrationCode, EntityName, RecordId, FieldName, CanonicalValue, LegacyValue)
        SELECT N'DELICARE_CORE_V2_20261113', N'Supplier', c.Id, v.FieldName,
               v.CanonicalValue, v.LegacyValue
        FROM dbo.Supplier c
        INNER JOIN dbo.Suppliers s ON s.Id = c.Id
        CROSS APPLY (VALUES
            (N'Name',        CONVERT(nvarchar(max), c.Name),        CONVERT(nvarchar(max), s.Name)),
            (N'Code',        CONVERT(nvarchar(max), c.Code),        CONVERT(nvarchar(max), s.Code)),
            (N'SupplierType',CONVERT(nvarchar(max), c.SupplierType),CONVERT(nvarchar(max), s.SupplierType)),
            (N'TaxCode',     CONVERT(nvarchar(max), c.TaxCode),     CONVERT(nvarchar(max), s.TaxCode)),
            (N'Address',     CONVERT(nvarchar(max), c.Address),     CONVERT(nvarchar(max), s.Address)),
            (N'Phone',       CONVERT(nvarchar(max), c.Phone),       CONVERT(nvarchar(max), s.Phone)),
            (N'Email',       CONVERT(nvarchar(max), c.Email),       CONVERT(nvarchar(max), s.Email)),
            (N'Status',      CONVERT(nvarchar(max), c.Status),      CONVERT(nvarchar(max), s.Status)),
            (N'CompanyId',   CONVERT(nvarchar(max), c.CompanyId),   CONVERT(nvarchar(max), s.CompanyId)),
            (N'ContactName', CONVERT(nvarchar(max), c.ContactName), CONVERT(nvarchar(max), s.ContactName))
        ) v(FieldName, CanonicalValue, LegacyValue)
        WHERE v.CanonicalValue IS NOT NULL
          AND v.LegacyValue IS NOT NULL
          AND v.CanonicalValue <> v.LegacyValue
          AND NOT EXISTS
          (
              SELECT 1 FROM dbo.CoreDataMergeConflict x
              WHERE x.MigrationCode = N'DELICARE_CORE_V2_20261113'
                AND x.EntityName = N'Supplier'
                AND x.RecordId = c.Id
                AND x.FieldName = v.FieldName
          );

        /* Copy missing IDs from plural table. */
        INSERT dbo.Supplier
        (
            Id, Name, Note, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy,
            DeletedDate, DeleteddBy, IsDeleted, Code, SupplierType, TaxCode,
            Address, Phone, Email, Status, CompanyId, ContactName, DeletedBy
        )
        SELECT
            s.Id, s.Name, s.Note, s.CreatedDate, s.CreatedBy, s.ModifiedDate,
            s.ModifiedBy, s.DeletedDate, COALESCE(s.DeleteddBy, s.DeletedBy),
            s.IsDeleted, s.Code, s.SupplierType, s.TaxCode, s.Address, s.Phone,
            s.Email, s.Status, s.CompanyId, s.ContactName,
            COALESCE(s.DeletedBy, s.DeleteddBy)
        FROM dbo.Suppliers s
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Supplier c WHERE c.Id = s.Id);

        /* Same Id: fill only NULL/empty canonical fields; never silently replace
           an already populated canonical business value. */
        UPDATE c
        SET
            c.Name         = COALESCE(NULLIF(c.Name, N''), s.Name),
            c.Note         = COALESCE(NULLIF(c.Note, N''), s.Note),
            c.Code         = COALESCE(NULLIF(c.Code, N''), s.Code),
            c.SupplierType = COALESCE(NULLIF(c.SupplierType, N''), s.SupplierType),
            c.TaxCode      = COALESCE(NULLIF(c.TaxCode, N''), s.TaxCode),
            c.Address      = COALESCE(NULLIF(c.Address, N''), s.Address),
            c.Phone        = COALESCE(NULLIF(c.Phone, N''), s.Phone),
            c.Email        = COALESCE(NULLIF(c.Email, N''), s.Email),
            c.Status       = COALESCE(NULLIF(c.Status, N''), s.Status),
            c.CompanyId    = COALESCE(c.CompanyId, s.CompanyId),
            c.ContactName  = COALESCE(NULLIF(c.ContactName, N''), s.ContactName),
            c.ModifiedDate = COALESCE(c.ModifiedDate, s.ModifiedDate),
            c.ModifiedBy   = COALESCE(c.ModifiedBy, s.ModifiedBy),
            c.DeletedDate  = COALESCE(c.DeletedDate, s.DeletedDate),
            c.DeletedBy    = COALESCE(c.DeletedBy, s.DeletedBy, s.DeleteddBy),
            c.DeleteddBy   = COALESCE(c.DeleteddBy, s.DeleteddBy, s.DeletedBy),
            c.IsDeleted    = CASE WHEN c.IsDeleted = 1 OR s.IsDeleted = 1 THEN 1 ELSE 0 END
        FROM dbo.Supplier c
        INNER JOIN dbo.Suppliers s ON s.Id = c.Id;

        EXEC sys.sp_rename N'dbo.Suppliers', N'Suppliers_Legacy_20261113', N'OBJECT';
    END; */

    /* =========================================================================
       04B. UNITMEASURE + UNITSOFMEASURE -> dbo.UnitMeasure
       ========================================================================= */
    IF OBJECT_ID(N'dbo.UnitsOfMeasure', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.UnitsOfMeasure_Legacy_20261113', N'U') IS NOT NULL
            THROW 51007, 'UnitsOfMeasure legacy archive already exists. Inspect before retrying.', 1;

        IF COL_LENGTH(N'dbo.UnitMeasure', N'Code') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD Code nvarchar(50) NULL;
        IF COL_LENGTH(N'dbo.UnitMeasure', N'UomType') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD UomType nvarchar(100) NULL;
        IF COL_LENGTH(N'dbo.UnitMeasure', N'DecimalPrecision') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD DecimalPrecision int NULL;
        IF COL_LENGTH(N'dbo.UnitMeasure', N'Status') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD Status nvarchar(50) NULL;
        IF COL_LENGTH(N'dbo.UnitMeasure', N'Symbol') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD Symbol nvarchar(max) NULL;
        IF COL_LENGTH(N'dbo.UnitMeasure', N'UnitType') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD UnitType nvarchar(max) NULL;
        IF COL_LENGTH(N'dbo.UnitMeasure', N'DeletedBy') IS NULL
            ALTER TABLE dbo.UnitMeasure ADD DeletedBy uniqueidentifier NULL;
/*
        INSERT dbo.CoreDataMergeConflict
            (MigrationCode, EntityName, RecordId, FieldName, CanonicalValue, LegacyValue)
        SELECT N'DELICARE_CORE_V2_20261113', N'UnitMeasure', c.Id, v.FieldName,
               v.CanonicalValue, v.LegacyValue
        FROM dbo.UnitMeasure c
        INNER JOIN dbo.UnitsOfMeasure u ON u.Id = c.Id
        CROSS APPLY (VALUES
            (N'Name', CONVERT(nvarchar(max), c.Name), CONVERT(nvarchar(max), u.Name)),
            (N'Code', CONVERT(nvarchar(max), c.Code), CONVERT(nvarchar(max), u.Code)),
            (N'UomType', CONVERT(nvarchar(max), c.UomType), CONVERT(nvarchar(max), u.UomType)),
            (N'DecimalPrecision', CONVERT(nvarchar(max), c.DecimalPrecision), CONVERT(nvarchar(max), u.DecimalPrecision)),
            (N'Status', CONVERT(nvarchar(max), c.Status), CONVERT(nvarchar(max), u.Status)),
            (N'Symbol', CONVERT(nvarchar(max), c.Symbol), CONVERT(nvarchar(max), u.Symbol)),
            (N'UnitType', CONVERT(nvarchar(max), c.UnitType), CONVERT(nvarchar(max), u.UnitType))
        ) v(FieldName, CanonicalValue, LegacyValue)
        WHERE v.CanonicalValue IS NOT NULL
          AND v.LegacyValue IS NOT NULL
          AND v.CanonicalValue <> v.LegacyValue
          AND NOT EXISTS
          (
              SELECT 1 FROM dbo.CoreDataMergeConflict x
              WHERE x.MigrationCode = N'DELICARE_CORE_V2_20261113'
                AND x.EntityName = N'UnitMeasure'
                AND x.RecordId = c.Id
                AND x.FieldName = v.FieldName
          );

     --INSERT dbo.UnitMeasure
     --(
     --    Id, Name, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy,
     --    DeletedDate, DeleteddBy, IsDeleted, Code, UomType,
     --    DecimalPrecision, Status, Symbol, UnitType, DeletedBy
     --)
     --SELECT
     --    u.Id, u.Name, u.CreatedDate, u.CreatedBy, u.ModifiedDate, u.ModifiedBy,
     --    u.DeletedDate, COALESCE(u.DeleteddBy, u.DeletedBy), u.IsDeleted,
     --    u.Code, u.UomType, u.DecimalPrecision, u.Status, u.Symbol, 
     --    COALESCE(u.DeletedBy, u.DeleteddBy)
     --FROM dbo.UnitsOfMeasure u
     --WHERE NOT EXISTS (SELECT 1 FROM dbo.UnitMeasure c WHERE c.Id = u.Id);

        UPDATE c
        SET
            c.Name             = COALESCE(NULLIF(c.Name, N''), u.Name),
            c.Code             = COALESCE(NULLIF(c.Code, N''), u.Code),
            c.UomType          = COALESCE(NULLIF(c.UomType, N''), u.UomType),
            c.DecimalPrecision = COALESCE(c.DecimalPrecision, u.DecimalPrecision),
            c.Status           = COALESCE(NULLIF(c.Status, N''), u.Status),
            c.Symbol           = COALESCE(NULLIF(c.Symbol, N''), u.Symbol),
            c.UnitType         = COALESCE(NULLIF(c.UnitType, N''), u.UnitType),
            c.ModifiedDate     = COALESCE(c.ModifiedDate, u.ModifiedDate),
            c.ModifiedBy       = COALESCE(c.ModifiedBy, u.ModifiedBy),
            c.DeletedDate      = COALESCE(c.DeletedDate, u.DeletedDate),
            c.DeletedBy        = COALESCE(c.DeletedBy, u.DeletedBy, u.DeleteddBy),
            c.DeleteddBy       = COALESCE(c.DeleteddBy, u.DeleteddBy, u.DeletedBy),
            c.IsDeleted        = CASE WHEN c.IsDeleted = 1 OR u.IsDeleted = 1 THEN 1 ELSE 0 END
        FROM dbo.UnitMeasure c
        INNER JOIN dbo.UnitsOfMeasure u ON u.Id = c.Id;
        */
        EXEC sys.sp_rename N'dbo.UnitsOfMeasure', N'UnitsOfMeasure_Legacy_20261113', N'OBJECT';
    END;

    /* =========================================================================
       04C. ACTUALCOST + ACTUALCOSTS -> dbo.ActualCost
       Source definitions are structurally identical (19 columns incl. Id).
       ========================================================================= */
    IF OBJECT_ID(N'dbo.ActualCosts', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.ActualCosts_Legacy_20261113', N'U') IS NOT NULL
            THROW 51008, 'ActualCosts legacy archive already exists. Inspect before retrying.', 1;

        INSERT dbo.CoreDataMergeConflict
            (MigrationCode, EntityName, RecordId, FieldName, CanonicalValue, LegacyValue)
        SELECT N'DELICARE_CORE_V2_20261113', N'ActualCost', c.Id, v.FieldName,
               v.CanonicalValue, v.LegacyValue
        FROM dbo.ActualCost c
        INNER JOIN dbo.ActualCosts a ON a.Id = c.Id
        CROSS APPLY (VALUES
            (N'ProductionBatchId', CONVERT(nvarchar(max), c.ProductionBatchId), CONVERT(nvarchar(max), a.ProductionBatchId)),
            (N'ProductionOrderId', CONVERT(nvarchar(max), c.ProductionOrderId), CONVERT(nvarchar(max), a.ProductionOrderId)),
            (N'MaterialCost', CONVERT(nvarchar(max), c.MaterialCost), CONVERT(nvarchar(max), a.MaterialCost)),
            (N'LaborCost', CONVERT(nvarchar(max), c.LaborCost), CONVERT(nvarchar(max), a.LaborCost)),
            (N'OverheadCost', CONVERT(nvarchar(max), c.OverheadCost), CONVERT(nvarchar(max), a.OverheadCost)),
            (N'WasteCost', CONVERT(nvarchar(max), c.WasteCost), CONVERT(nvarchar(max), a.WasteCost)),
            (N'TotalCost', CONVERT(nvarchar(max), c.TotalCost), CONVERT(nvarchar(max), a.TotalCost)),
            (N'OtherCost', CONVERT(nvarchar(max), c.OtherCost), CONVERT(nvarchar(max), a.OtherCost)),
            (N'CurrencyCode', CONVERT(nvarchar(max), c.CurrencyCode), CONVERT(nvarchar(max), a.CurrencyCode)),
            (N'CalculatedAt', CONVERT(nvarchar(max), c.CalculatedAt), CONVERT(nvarchar(max), a.CalculatedAt))
        ) v(FieldName, CanonicalValue, LegacyValue)
        WHERE v.CanonicalValue IS NOT NULL
          AND v.LegacyValue IS NOT NULL
          AND v.CanonicalValue <> v.LegacyValue
          AND NOT EXISTS
          (
              SELECT 1 FROM dbo.CoreDataMergeConflict x
              WHERE x.MigrationCode = N'DELICARE_CORE_V2_20261113'
                AND x.EntityName = N'ActualCost'
                AND x.RecordId = c.Id
                AND x.FieldName = v.FieldName
          );

        INSERT dbo.ActualCost
        (
            Id, ProductionBatchId, MaterialCost, LaborCost, OverheadCost, WasteCost,
            TotalCost, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy, DeletedDate,
            DeletedBy, IsDeleted, ProductionOrderId, OtherCost, CurrencyCode,
            CalculatedAt, DeleteddBy
        )
        SELECT
            a.Id, a.ProductionBatchId, a.MaterialCost, a.LaborCost, a.OverheadCost,
            a.WasteCost, a.TotalCost, a.CreatedDate, a.CreatedBy, a.ModifiedDate,
            a.ModifiedBy, a.DeletedDate, a.DeletedBy, a.IsDeleted,
            a.ProductionOrderId, a.OtherCost, a.CurrencyCode, a.CalculatedAt,
            a.DeleteddBy
        FROM dbo.ActualCosts a
        WHERE NOT EXISTS (SELECT 1 FROM dbo.ActualCost c WHERE c.Id = a.Id);

        UPDATE c
        SET
            c.ProductionBatchId = COALESCE(c.ProductionBatchId, a.ProductionBatchId),
            c.ProductionOrderId = COALESCE(c.ProductionOrderId, a.ProductionOrderId),
            c.MaterialCost      = COALESCE(c.MaterialCost, a.MaterialCost),
            c.LaborCost         = COALESCE(c.LaborCost, a.LaborCost),
            c.OverheadCost      = COALESCE(c.OverheadCost, a.OverheadCost),
            c.WasteCost         = COALESCE(c.WasteCost, a.WasteCost),
            c.TotalCost         = COALESCE(c.TotalCost, a.TotalCost),
            c.OtherCost         = COALESCE(c.OtherCost, a.OtherCost),
            c.CurrencyCode      = COALESCE(c.CurrencyCode, a.CurrencyCode),
            c.CalculatedAt      = COALESCE(c.CalculatedAt, a.CalculatedAt),
            c.ModifiedDate      = COALESCE(c.ModifiedDate, a.ModifiedDate),
            c.ModifiedBy        = COALESCE(c.ModifiedBy, a.ModifiedBy),
            c.DeletedDate       = COALESCE(c.DeletedDate, a.DeletedDate),
            c.DeletedBy         = COALESCE(c.DeletedBy, a.DeletedBy, a.DeleteddBy),
            c.DeleteddBy        = COALESCE(c.DeleteddBy, a.DeleteddBy, a.DeletedBy),
            c.IsDeleted         = CASE WHEN c.IsDeleted = 1 OR a.IsDeleted = 1 THEN 1 ELSE 0 END
        FROM dbo.ActualCost c
        INNER JOIN dbo.ActualCosts a ON a.Id = c.Id;

        EXEC sys.sp_rename N'dbo.ActualCosts', N'ActualCosts_Legacy_20261113', N'OBJECT';
    END;

    /* 04D. Legacy object names continue to resolve to canonical tables.
       Note: synonyms are not physical tables and cannot be FK targets. */
   /* IF NOT EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'Suppliers')
        EXEC(N'CREATE SYNONYM dbo.Suppliers FOR dbo.Supplier;');
    IF NOT EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'UnitsOfMeasure')
        EXEC(N'CREATE SYNONYM dbo.UnitsOfMeasure FOR dbo.UnitMeasure;');
    IF NOT EXISTS (SELECT 1 FROM sys.synonyms WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'ActualCosts')
        EXEC(N'CREATE SYNONYM dbo.ActualCosts FOR dbo.ActualCost;');
        */
    /* 04E. No domain schemas or domain synonyms are created.
       All physical tables and no reference crosswalk is created. */

    /* Reference architecture is external design input only.
       No reference objects or crosswalk rows are created in AppDelicare. */

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* 05. POST-MIGRATION VALIDATION */
SELECT N'Supplier' AS CanonicalTable, COUNT_BIG(*) FROM dbo.Supplier
UNION ALL
SELECT N'UnitMeasure', COUNT_BIG(*) FROM dbo.UnitMeasure
UNION ALL
SELECT N'ActualCost', COUNT_BIG(*) FROM dbo.ActualCost;
GO

SELECT *
FROM dbo.CoreDataMergeConflict
WHERE MigrationCode = N'DELICARE_CORE_V2_20261113'
ORDER BY EntityName, RecordId, FieldName;
GO

/* Review duplicate business keys; this query does not delete or merge records. */
SELECT Code, COUNT(*) AS DuplicateCount
FROM dbo.Supplier
WHERE NULLIF(LTRIM(RTRIM(Code)), N'') IS NOT NULL AND ISNULL(IsDeleted, 0) = 0
GROUP BY Code HAVING COUNT(*) > 1;

SELECT TaxCode, COUNT(*) AS DuplicateCount
FROM dbo.Supplier
WHERE NULLIF(LTRIM(RTRIM(TaxCode)), N'') IS NOT NULL AND ISNULL(IsDeleted, 0) = 0
GROUP BY TaxCode HAVING COUNT(*) > 1;

--SELECT Code, COUNT(*) AS DuplicateCount
--FROM dbo.UnitMeasure
--WHERE NULLIF(LTRIM(RTRIM(Code)), N'') IS NOT NULL AND ISNULL(IsDeleted, 0) = 0
--GROUP BY Code HAVING COUNT(*) > 1;
--GO

/* Confirm legacy compatibility aliases. All synonyms remain in dbo. */
SELECT s.name AS SchemaName, sy.name AS SynonymName, sy.base_object_name AS TargetObject
FROM sys.synonyms sy
JOIN sys.schemas s ON s.schema_id = sy.schema_id
WHERE s.name = N'dbo'
  AND sy.name IN (N'Suppliers', N'UnitsOfMeasure', N'ActualCosts')
ORDER BY sy.name;
GO

/* Confirm there is no physical user schema other than dbo (system schemas excluded). */
SELECT name AS UserSchema
FROM sys.schemas
WHERE name NOT IN (N'dbo', N'guest', N'sys', N'INFORMATION_SCHEMA')
ORDER BY name;
GO

/* Review candidate FK columns. Neither supplied source script defines FK
   constraints. This query reports candidate columns; it does not create FKs. */
SELECT
    OBJECT_SCHEMA_NAME(c.object_id) AS TableSchema,
    OBJECT_NAME(c.object_id) AS TableName,
    c.name AS CandidateReferenceColumn,
    TYPE_NAME(c.user_type_id) AS DataType,
    c.is_nullable
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
WHERE c.name IN (N'SupplierId', N'UnitMeasureId', N'CustomerId', N'IngredientId',
                 N'WarehouseId', N'ProductionBatchId', N'ProductionOrderId')
ORDER BY c.name, TableName;
GO

/*
PHASE 1 SCOPE NOTE
The operational capabilities below are added in Phases 2-4 of THIS same file.
They are normalized extensions designed around actual AppDelicare entities;
they are not copied as physical tables from the reference architecture.

DEFERRED CLEANUP (not performed automatically):
  - Update .NET entities and EF mappings to canonical singular names.
  - Replace misspelled existing names (Dishe, DishCategorie, ProductionBatche,
    CleaningBatche, ProofOfDeliverie) only after a full dependency scan.
  - Standardize DeleteddBy -> DeletedBy only after source-code dependency scan.
  - Review unrelated clinic/demo candidates at the end of this script.
  - Existing core-table FK constraints are not bulk-created in this release.
    section after the new table structures are available.
*/


/* ==========================================================================
   PHASE 2 - OPERATIONAL CAPABILITIES
   ========================================================================== */
IF OBJECT_ID(N'dbo.ProductionLine', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductionLine
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ProductionLine_Id DEFAULT NEWSEQUENTIALID(),
        Code nvarchar(50) NOT NULL,
        Name nvarchar(255) NOT NULL,
        CapacityMealPerBatch decimal(18,2) NULL,
        MinutesPerBatch decimal(10,2) NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_ProductionLine_Status DEFAULT (N'Active'),
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ProductionLine_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ProductionLine_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ProductionLine PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ProductionLine_Code UNIQUE (Code)
    );
END;
GO
IF OBJECT_ID(N'dbo.KitchenWorkstation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KitchenWorkstation
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_KitchenWorkstation_Id DEFAULT NEWSEQUENTIALID(),
        Code nvarchar(50) NOT NULL,
        Name nvarchar(255) NOT NULL,
        AreaName nvarchar(255) NULL,
        Latitude decimal(10,7) NULL,
        Longitude decimal(10,7) NULL,
        RadiusMeters decimal(10,2) NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_KitchenWorkstation_Status DEFAULT (N'Active'),
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_KitchenWorkstation_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_KitchenWorkstation_IsDeleted DEFAULT (0),
        CONSTRAINT PK_KitchenWorkstation PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_KitchenWorkstation_Code UNIQUE (Code)
    );
END;
GO
/* Recipe versioning uses the existing dbo.Recipe entity. */
IF OBJECT_ID(N'dbo.ShiftAssignment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShiftAssignment
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ShiftAssignment_Id DEFAULT NEWSEQUENTIALID(),
        EmployeeId uniqueidentifier NOT NULL,
        ShiftId uniqueidentifier NOT NULL,
        WorkDate date NOT NULL,
        PlannedStart datetime2(0) NULL,
        PlannedEnd datetime2(0) NULL,
        AssignmentStatus nvarchar(30) NOT NULL CONSTRAINT DF_ShiftAssignment_Status DEFAULT (N'Planned'),
        AssignedBy uniqueidentifier NULL,
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ShiftAssignment_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ShiftAssignment_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ShiftAssignment PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ShiftAssignment_Employee_Shift_Date UNIQUE (EmployeeId, ShiftId, WorkDate)
    );
END;
GO
IF OBJECT_ID(N'dbo.AttendanceEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AttendanceEvent
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_AttendanceEvent_Id DEFAULT NEWSEQUENTIALID(),
        EmployeeId uniqueidentifier NOT NULL,
        ShiftAssignmentId uniqueidentifier NULL,
        EventType nvarchar(30) NOT NULL,
        EventAt datetime2(0) NOT NULL,
        Latitude decimal(10,7) NULL,
        Longitude decimal(10,7) NULL,
        DistanceMeters decimal(10,2) NULL,
        IsValid bit NULL,
        SourceChannel nvarchar(50) NULL,
        ExternalEventId nvarchar(200) NULL,
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_AttendanceEvent_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_AttendanceEvent_IsDeleted DEFAULT (0),
        CONSTRAINT PK_AttendanceEvent PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.FoodSafetyChecklist', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FoodSafetyChecklist
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_FoodSafetyChecklist_Id DEFAULT NEWSEQUENTIALID(),
        Code nvarchar(50) NOT NULL,
        Name nvarchar(255) NOT NULL,
        VersionNo int NOT NULL CONSTRAINT DF_FoodSafetyChecklist_Version DEFAULT (1),
        AreaCode nvarchar(50) NULL,
        FrequencyCode nvarchar(30) NULL,
        EffectiveFrom date NULL,
        EffectiveTo date NULL,
        Status nvarchar(30) NOT NULL CONSTRAINT DF_FoodSafetyChecklist_Status DEFAULT (N'Active'),
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_FoodSafetyChecklist_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_FoodSafetyChecklist_IsDeleted DEFAULT (0),
        CONSTRAINT PK_FoodSafetyChecklist PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_FoodSafetyChecklist_Code_Version UNIQUE (Code, VersionNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.FoodSafetyChecklistItem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FoodSafetyChecklistItem
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_FoodSafetyChecklistItem_Id DEFAULT NEWSEQUENTIALID(),
        ChecklistId uniqueidentifier NOT NULL,
        ItemCode nvarchar(50) NOT NULL,
        ItemName nvarchar(500) NOT NULL,
        Description nvarchar(1000) NULL,
        SortOrder int NOT NULL CONSTRAINT DF_FoodSafetyChecklistItem_Sort DEFAULT (0),
        IsCritical bit NOT NULL CONSTRAINT DF_FoodSafetyChecklistItem_Critical DEFAULT (0),
        ExpectedValue nvarchar(500) NULL,
        UnitCode nvarchar(30) NULL,
        IsRequired bit NOT NULL CONSTRAINT DF_FoodSafetyChecklistItem_Required DEFAULT (1),
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_FoodSafetyChecklistItem_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_FoodSafetyChecklistItem_IsDeleted DEFAULT (0),
        CONSTRAINT PK_FoodSafetyChecklistItem PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_FoodSafetyChecklistItem_Checklist_Item UNIQUE (ChecklistId, ItemCode)
    );
END;
GO
IF OBJECT_ID(N'dbo.FoodSafetyChecklistResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FoodSafetyChecklistResult
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_FoodSafetyChecklistResult_Id DEFAULT NEWSEQUENTIALID(),
        ChecklistId uniqueidentifier NOT NULL,
        ChecklistItemId uniqueidentifier NOT NULL,
        CheckDate date NOT NULL,
        ShiftId uniqueidentifier NULL,
        ProductionBatchId uniqueidentifier NULL,
        WorkstationId uniqueidentifier NULL,
        ResultCode nvarchar(30) NOT NULL,
        ActualValue nvarchar(500) NULL,
        CheckedBy uniqueidentifier NULL,
        CheckedAt datetime2(0) NOT NULL CONSTRAINT DF_FoodSafetyChecklistResult_CheckedAt DEFAULT SYSDATETIME(),
        CorrectiveAction nvarchar(2000) NULL,
        EvidenceUrl nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_FoodSafetyChecklistResult_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_FoodSafetyChecklistResult_IsDeleted DEFAULT (0),
        CONSTRAINT PK_FoodSafetyChecklistResult PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.TemperatureLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TemperatureLog
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_TemperatureLog_Id DEFAULT NEWSEQUENTIALID(),
        StorageZoneId uniqueidentifier NULL,
        StorageLocationId uniqueidentifier NULL,
        MaterialLotId uniqueidentifier NULL,
        ProductionBatchId uniqueidentifier NULL,
        MeasurementType nvarchar(40) NOT NULL,
        TemperatureC decimal(6,2) NOT NULL,
        MinAllowedC decimal(6,2) NULL,
        MaxAllowedC decimal(6,2) NULL,
        IsWithinRange bit NULL,
        MeasuredAt datetime2(0) NOT NULL CONSTRAINT DF_TemperatureLog_MeasuredAt DEFAULT SYSDATETIME(),
        DeviceCode nvarchar(100) NULL,
        SourceChannel nvarchar(50) NULL,
        RecordedBy uniqueidentifier NULL,
        CorrectiveAction nvarchar(2000) NULL,
        EvidenceUrl nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_TemperatureLog_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_TemperatureLog_IsDeleted DEFAULT (0),
        CONSTRAINT PK_TemperatureLog PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.DeliveryStop', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryStop
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_DeliveryStop_Id DEFAULT NEWSEQUENTIALID(),
        DeliveryRouteId uniqueidentifier NOT NULL,
        DeliveryOrderId uniqueidentifier NULL,
        CustomerId uniqueidentifier NULL,
        DeliveryLocationId uniqueidentifier NULL,
        SequenceNo int NOT NULL,
        PlannedArrival datetime2(0) NULL,
        ActualArrival datetime2(0) NULL,
        PlannedMealQty decimal(18,3) NULL,
        DeliveredMealQty decimal(18,3) NULL,
        ShortageQty decimal(18,3) NULL,
        StopStatus nvarchar(30) NOT NULL CONSTRAINT DF_DeliveryStop_Status DEFAULT (N'Planned'),
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_DeliveryStop_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_DeliveryStop_IsDeleted DEFAULT (0),
        CONSTRAINT PK_DeliveryStop PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_DeliveryStop_Route_Sequence UNIQUE (DeliveryRouteId, SequenceNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.DeliveryEvent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeliveryEvent
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_DeliveryEvent_Id DEFAULT NEWSEQUENTIALID(),
        DeliveryRouteId uniqueidentifier NOT NULL,
        DeliveryStopId uniqueidentifier NULL,
        DeliveryOrderId uniqueidentifier NULL,
        EventType nvarchar(50) NOT NULL,
        EventAt datetime2(0) NOT NULL CONSTRAINT DF_DeliveryEvent_EventAt DEFAULT SYSDATETIME(),
        Latitude decimal(10,7) NULL,
        Longitude decimal(10,7) NULL,
        TemperatureC decimal(6,2) NULL,
        PhotoUrl nvarchar(1000) NULL,
        Description nvarchar(2000) NULL,
        RecordedBy uniqueidentifier NULL,
        SourceChannel nvarchar(50) NULL,
        ExternalEventId nvarchar(200) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_DeliveryEvent_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_DeliveryEvent_IsDeleted DEFAULT (0),
        CONSTRAINT PK_DeliveryEvent PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.PackagingLedger', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PackagingLedger
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_PackagingLedger_Id DEFAULT NEWSEQUENTIALID(),
        CustomerId uniqueidentifier NULL,
        DeliveryLocationId uniqueidentifier NULL,
        OrderId uniqueidentifier NULL,
        DeliveryRouteId uniqueidentifier NULL,
        TransactionType nvarchar(30) NOT NULL,
        PackagingType nvarchar(100) NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        TransactionAt datetime2(0) NOT NULL CONSTRAINT DF_PackagingLedger_At DEFAULT SYSDATETIME(),
        ReferenceNo nvarchar(100) NULL,
        ConditionCode nvarchar(30) NULL,
        RecordedBy uniqueidentifier NULL,
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_PackagingLedger_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_PackagingLedger_IsDeleted DEFAULT (0),
        CONSTRAINT PK_PackagingLedger PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.MealWasteRecord', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealWasteRecord
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_MealWasteRecord_Id DEFAULT NEWSEQUENTIALID(),
        ProductionBatchId uniqueidentifier NULL,
        ProductionOrderId uniqueidentifier NULL,
        DishId uniqueidentifier NULL,
        ShiftId uniqueidentifier NULL,
        WasteType nvarchar(50) NOT NULL,
        QuantityMeals decimal(18,3) NULL,
        QuantityKg decimal(18,3) NULL,
        Reason nvarchar(1000) NULL,
        Disposition nvarchar(500) NULL,
        OccurredAt datetime2(0) NOT NULL CONSTRAINT DF_MealWasteRecord_OccurredAt DEFAULT SYSDATETIME(),
        RecordedBy uniqueidentifier NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_MealWasteRecord_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_MealWasteRecord_IsDeleted DEFAULT (0),
        CONSTRAINT PK_MealWasteRecord PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.SupplierIssue', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SupplierIssue
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_SupplierIssue_Id DEFAULT NEWSEQUENTIALID(),
        SupplierId uniqueidentifier NOT NULL,
        PurchaseOrderId uniqueidentifier NULL,
        GoodsReceiptId uniqueidentifier NULL,
        IssueType nvarchar(50) NOT NULL,
        Severity nvarchar(30) NOT NULL CONSTRAINT DF_SupplierIssue_Severity DEFAULT (N'Medium'),
        Description nvarchar(2000) NOT NULL,
        ReportedAt datetime2(0) NOT NULL CONSTRAINT DF_SupplierIssue_ReportedAt DEFAULT SYSDATETIME(),
        DueAt datetime2(0) NULL,
        IssueStatus nvarchar(30) NOT NULL CONSTRAINT DF_SupplierIssue_Status DEFAULT (N'Open'),
        Resolution nvarchar(2000) NULL,
        ClosedAt datetime2(0) NULL,
        OwnerId uniqueidentifier NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_SupplierIssue_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_SupplierIssue_IsDeleted DEFAULT (0),
        CONSTRAINT PK_SupplierIssue PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.CustomerComplaint', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerComplaint
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_CustomerComplaint_Id DEFAULT NEWSEQUENTIALID(),
        ComplaintNo nvarchar(50) NOT NULL,
        CustomerId uniqueidentifier NOT NULL,
        OrderId uniqueidentifier NULL,
        DeliveryOrderId uniqueidentifier NULL,
        DeliveryRouteId uniqueidentifier NULL,
        SourceChannel nvarchar(50) NULL,
        CategoryCode nvarchar(50) NULL,
        Severity nvarchar(30) NOT NULL CONSTRAINT DF_CustomerComplaint_Severity DEFAULT (N'Medium'),
        Description nvarchar(2000) NOT NULL,
        ReceivedAt datetime2(0) NOT NULL CONSTRAINT DF_CustomerComplaint_ReceivedAt DEFAULT SYSDATETIME(),
        SlaDueAt datetime2(0) NULL,
        ComplaintStatus nvarchar(30) NOT NULL CONSTRAINT DF_CustomerComplaint_Status DEFAULT (N'Open'),
        AssignedTo uniqueidentifier NULL,
        ResolvedAt datetime2(0) NULL,
        Resolution nvarchar(2000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_CustomerComplaint_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_CustomerComplaint_IsDeleted DEFAULT (0),
        CONSTRAINT PK_CustomerComplaint PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_CustomerComplaint_ComplaintNo UNIQUE (ComplaintNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.ComplaintHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplaintHistory
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ComplaintHistory_Id DEFAULT NEWSEQUENTIALID(),
        ComplaintId uniqueidentifier NOT NULL,
        EventType nvarchar(50) NOT NULL,
        OldStatus nvarchar(30) NULL,
        NewStatus nvarchar(30) NULL,
        Comment nvarchar(2000) NULL,
        EventAt datetime2(0) NOT NULL CONSTRAINT DF_ComplaintHistory_EventAt DEFAULT SYSDATETIME(),
        ActorId uniqueidentifier NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ComplaintHistory_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ComplaintHistory_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ComplaintHistory PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.CleaningChecklistResult', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CleaningChecklistResult
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_CleaningChecklistResult_Id DEFAULT NEWSEQUENTIALID(),
        CleaningBatchId uniqueidentifier NULL,
        WorkstationId uniqueidentifier NULL,
        ChecklistId uniqueidentifier NULL,
        ChecklistItemId uniqueidentifier NULL,
        ResultCode nvarchar(30) NOT NULL,
        ActualValue nvarchar(500) NULL,
        CheckedAt datetime2(0) NOT NULL CONSTRAINT DF_CleaningChecklistResult_CheckedAt DEFAULT SYSDATETIME(),
        CheckedBy uniqueidentifier NULL,
        ChemicalUsed nvarchar(255) NULL,
        CorrectiveAction nvarchar(2000) NULL,
        EvidenceUrl nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_CleaningChecklistResult_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_CleaningChecklistResult_IsDeleted DEFAULT (0),
        CONSTRAINT PK_CleaningChecklistResult PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.MenuChangeRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuChangeRequest
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_MenuChangeRequest_Id DEFAULT NEWSEQUENTIALID(),
        RequestNo nvarchar(50) NOT NULL,
        OrderId uniqueidentifier NULL,
        MenuId uniqueidentifier NULL,
        OriginalDishId uniqueidentifier NULL,
        RequestedDishId uniqueidentifier NULL,
        MealDate date NOT NULL,
        ShiftId uniqueidentifier NULL,
        Quantity decimal(18,3) NULL,
        Reason nvarchar(1000) NOT NULL,
        RequestedBy uniqueidentifier NULL,
        RequestStatus nvarchar(30) NOT NULL CONSTRAINT DF_MenuChangeRequest_Status DEFAULT (N'Pending'),
        ApprovedBy uniqueidentifier NULL,
        ApprovedAt datetime2(0) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_MenuChangeRequest_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_MenuChangeRequest_IsDeleted DEFAULT (0),
        CONSTRAINT PK_MenuChangeRequest PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_MenuChangeRequest_RequestNo UNIQUE (RequestNo)
    );
END;
GO

/* ==========================================================================
   PHASE 2A - EXTEND EXISTING APPDELICARE TABLES
   ========================================================================== */
IF COL_LENGTH(N'dbo.Recipe', N'SnapshotJson') IS NULL
    ALTER TABLE dbo.Recipe ADD SnapshotJson nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.Recipe', N'LockedAt') IS NULL
    ALTER TABLE dbo.Recipe ADD LockedAt datetime2(0) NULL;
IF COL_LENGTH(N'dbo.Recipe', N'LockedBy') IS NULL
    ALTER TABLE dbo.Recipe ADD LockedBy uniqueidentifier NULL;

IF COL_LENGTH(N'dbo.ProductionOrder', N'ProductionLineId') IS NULL
    ALTER TABLE dbo.ProductionOrder ADD ProductionLineId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.ProductionOrder', N'RecipeSnapshotJson') IS NULL
    ALTER TABLE dbo.ProductionOrder ADD RecipeSnapshotJson nvarchar(max) NULL;

IF COL_LENGTH(N'dbo.ProductionBatche', N'KitchenWorkstationId') IS NULL
    ALTER TABLE dbo.ProductionBatche ADD KitchenWorkstationId uniqueidentifier NULL;

IF COL_LENGTH(N'dbo.ProductionLine', N'CompanyId') IS NULL
    ALTER TABLE dbo.ProductionLine ADD CompanyId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.ProductionLine', N'OrganizationUnitId') IS NULL
    ALTER TABLE dbo.ProductionLine ADD OrganizationUnitId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.KitchenWorkstation', N'CompanyId') IS NULL
    ALTER TABLE dbo.KitchenWorkstation ADD CompanyId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.KitchenWorkstation', N'OrganizationUnitId') IS NULL
    ALTER TABLE dbo.KitchenWorkstation ADD OrganizationUnitId uniqueidentifier NULL;
GO

/* Migrate metadata from RecipeVersion if the previous draft was already run.
   Matching is by RecipeId + VersionNo; unmatched rows stop the migration. */
IF OBJECT_ID(N'dbo.RecipeVersion', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.RecipeVersion_Legacy_ReferenceOnly', N'U') IS NOT NULL
        THROW 51101, 'RecipeVersion legacy archive already exists. Review before rerunning.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.RecipeVersion rv
        LEFT JOIN dbo.Recipe r
          ON r.Id = rv.RecipeId AND r.VersionNo = rv.VersionNo
        WHERE r.Id IS NULL
    )
        THROW 51102, 'RecipeVersion has unmatched rows. Reconcile RecipeId + VersionNo before archiving.', 1;

    UPDATE r
       SET r.SnapshotJson = COALESCE(r.SnapshotJson, rv.SnapshotJson),
           r.LockedAt = COALESCE(r.LockedAt, rv.LockedAt),
           r.ApprovedBy = COALESCE(r.ApprovedBy, rv.ApprovedBy),
           r.ApprovedAt = COALESCE(r.ApprovedAt, rv.ApprovedAt),
           r.Note = COALESCE(r.Note, rv.Note)
    FROM dbo.Recipe r
    INNER JOIN dbo.RecipeVersion rv
      ON rv.RecipeId = r.Id AND rv.VersionNo = r.VersionNo;

    EXEC sys.sp_rename N'dbo.RecipeVersion', N'RecipeVersion_Legacy_ReferenceOnly', N'OBJECT';
END;
GO

/* Archive the old reference crosswalk if the earlier draft created it. */
IF OBJECT_ID(N'dbo.ArchitectureObjectMapping', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.ArchitectureObjectMapping_Legacy_ReferenceOnly', N'U') IS NOT NULL
        THROW 51103, 'ArchitectureObjectMapping archive already exists. Review before rerunning.', 1;
    EXEC sys.sp_rename N'dbo.ArchitectureObjectMapping',
                       N'ArchitectureObjectMapping_Legacy_ReferenceOnly', N'OBJECT';
END;
GO

/* ==========================================================================
   PHASE 3 - WORKFLOW AND MULTI-CHANNEL INTEGRATION
   ========================================================================== */
IF OBJECT_ID(N'dbo.ApprovalRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalRequest
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ApprovalRequest_Id DEFAULT NEWSEQUENTIALID(),
        RequestNo nvarchar(50) NOT NULL,
        BusinessType nvarchar(100) NOT NULL,
        BusinessId uniqueidentifier NULL,
        RequestedBy uniqueidentifier NULL,
        RequestedAt datetime2(0) NOT NULL CONSTRAINT DF_ApprovalRequest_RequestedAt DEFAULT SYSDATETIME(),
        RequestStatus nvarchar(30) NOT NULL CONSTRAINT DF_ApprovalRequest_Status DEFAULT (N'Pending'),
        CurrentStepNo int NULL,
        PriorityCode nvarchar(30) NULL,
        DueAt datetime2(0) NULL,
        PayloadJson nvarchar(max) NULL,
        CorrelationId nvarchar(100) NULL,
        CompletedAt datetime2(0) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ApprovalRequest_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ApprovalRequest_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ApprovalRequest PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ApprovalRequest_RequestNo UNIQUE (RequestNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.ApprovalStep', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalStep
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ApprovalStep_Id DEFAULT NEWSEQUENTIALID(),
        ApprovalRequestId uniqueidentifier NOT NULL,
        StepNo int NOT NULL,
        ApproverUserId uniqueidentifier NULL,
        ApproverRoleCode nvarchar(100) NULL,
        DecisionStatus nvarchar(30) NOT NULL CONSTRAINT DF_ApprovalStep_Status DEFAULT (N'Waiting'),
        AssignedAt datetime2(0) NULL,
        DueAt datetime2(0) NULL,
        DecidedAt datetime2(0) NULL,
        DecisionNote nvarchar(2000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ApprovalStep_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ApprovalStep_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ApprovalStep PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ApprovalStep_Request_Step UNIQUE (ApprovalRequestId, StepNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.ApprovalDecision', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalDecision
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ApprovalDecision_Id DEFAULT NEWSEQUENTIALID(),
        ApprovalRequestId uniqueidentifier NOT NULL,
        ApprovalStepId uniqueidentifier NOT NULL,
        DecisionCode nvarchar(30) NOT NULL,
        DecidedBy uniqueidentifier NOT NULL,
        DecidedAt datetime2(0) NOT NULL CONSTRAINT DF_ApprovalDecision_DecidedAt DEFAULT SYSDATETIME(),
        Comment nvarchar(2000) NULL,
        PayloadHash nvarchar(128) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ApprovalDecision_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ApprovalDecision_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ApprovalDecision PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.ApprovalItem', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalItem
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ApprovalItem_Id DEFAULT NEWSEQUENTIALID(),
        ApprovalRequestId uniqueidentifier NOT NULL,
        ItemType nvarchar(100) NOT NULL,
        ItemId uniqueidentifier NULL,
        ActionCode nvarchar(50) NOT NULL,
        PayloadJson nvarchar(max) NULL,
        ItemStatus nvarchar(30) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ApprovalItem_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ApprovalItem_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ApprovalItem PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.IntegrationChannel', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationChannel
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_IntegrationChannel_Id DEFAULT NEWSEQUENTIALID(),
        ChannelCode nvarchar(50) NOT NULL,
        ChannelName nvarchar(255) NOT NULL,
        ProviderCode nvarchar(50) NOT NULL,
        EnvironmentCode nvarchar(30) NOT NULL CONSTRAINT DF_IntegrationChannel_Environment DEFAULT (N'Production'),
        ChannelStatus nvarchar(30) NOT NULL CONSTRAINT DF_IntegrationChannel_Status DEFAULT (N'Inactive'),
        ConfigJson nvarchar(max) NULL,
        SecretReference nvarchar(500) NULL,
        OwnerUserId uniqueidentifier NULL,
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_IntegrationChannel_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_IntegrationChannel_IsDeleted DEFAULT (0),
        CONSTRAINT PK_IntegrationChannel PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_IntegrationChannel_Code UNIQUE (ChannelCode)
    );
END;
GO
IF OBJECT_ID(N'dbo.BotConfiguration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BotConfiguration
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_BotConfiguration_Id DEFAULT NEWSEQUENTIALID(),
        IntegrationChannelId uniqueidentifier NOT NULL,
        BotCode nvarchar(50) NOT NULL,
        BotName nvarchar(255) NOT NULL,
        BotUsername nvarchar(255) NULL,
        TargetAudience nvarchar(100) NULL,
        WebhookPath nvarchar(500) NULL,
        BotStatus nvarchar(30) NOT NULL CONSTRAINT DF_BotConfiguration_Status DEFAULT (N'Inactive'),
        LastTokenRotatedAt datetime2(0) NULL,
        SecretReference nvarchar(500) NULL,
        Note nvarchar(1000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_BotConfiguration_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_BotConfiguration_IsDeleted DEFAULT (0),
        CONSTRAINT PK_BotConfiguration PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_BotConfiguration_BotCode UNIQUE (BotCode)
    );
END;
GO
IF OBJECT_ID(N'dbo.UserExternalIdentity', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserExternalIdentity
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_UserExternalIdentity_Id DEFAULT NEWSEQUENTIALID(),
        UserId uniqueidentifier NULL,
        EmployeeId uniqueidentifier NULL,
        IntegrationChannelId uniqueidentifier NOT NULL,
        ExternalUserId nvarchar(255) NOT NULL,
        ExternalUsername nvarchar(255) NULL,
        DisplayName nvarchar(255) NULL,
        LinkedAt datetime2(0) NOT NULL CONSTRAINT DF_UserExternalIdentity_LinkedAt DEFAULT SYSDATETIME(),
        IdentityStatus nvarchar(30) NOT NULL CONSTRAINT DF_UserExternalIdentity_Status DEFAULT (N'Active'),
        LastSeenAt datetime2(0) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_UserExternalIdentity_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_UserExternalIdentity_IsDeleted DEFAULT (0),
        CONSTRAINT PK_UserExternalIdentity PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_UserExternalIdentity_Channel_User UNIQUE (IntegrationChannelId, ExternalUserId)
    );
END;
GO
IF OBJECT_ID(N'dbo.InboundMessage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InboundMessage
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_InboundMessage_Id DEFAULT NEWSEQUENTIALID(),
        IntegrationChannelId uniqueidentifier NOT NULL,
        ExternalMessageId nvarchar(255) NULL,
        ExternalConversationId nvarchar(255) NULL,
        ExternalUserId nvarchar(255) NULL,
        MessageType nvarchar(50) NULL,
        PayloadJson nvarchar(max) NULL,
        ReceivedAt datetime2(0) NOT NULL CONSTRAINT DF_InboundMessage_ReceivedAt DEFAULT SYSDATETIME(),
        ProcessingStatus nvarchar(30) NOT NULL CONSTRAINT DF_InboundMessage_Status DEFAULT (N'Received'),
        IdempotencyKey nvarchar(200) NULL,
        CorrelationId nvarchar(100) NULL,
        ErrorMessage nvarchar(2000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_InboundMessage_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_InboundMessage_IsDeleted DEFAULT (0),
        CONSTRAINT PK_InboundMessage PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.OutboundMessage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboundMessage
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_OutboundMessage_Id DEFAULT NEWSEQUENTIALID(),
        IntegrationChannelId uniqueidentifier NOT NULL,
        ExternalConversationId nvarchar(255) NULL,
        RecipientExternalId nvarchar(255) NULL,
        MessageType nvarchar(50) NULL,
        PayloadJson nvarchar(max) NOT NULL,
        QueuedAt datetime2(0) NOT NULL CONSTRAINT DF_OutboundMessage_QueuedAt DEFAULT SYSDATETIME(),
        SentAt datetime2(0) NULL,
        MessageStatus nvarchar(30) NOT NULL CONSTRAINT DF_OutboundMessage_Status DEFAULT (N'Queued'),
        RetryCount int NOT NULL CONSTRAINT DF_OutboundMessage_Retry DEFAULT (0),
        IdempotencyKey nvarchar(200) NULL,
        CorrelationId nvarchar(100) NULL,
        ProviderMessageId nvarchar(255) NULL,
        LastError nvarchar(2000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_OutboundMessage_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_OutboundMessage_IsDeleted DEFAULT (0),
        CONSTRAINT PK_OutboundMessage PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.IntegrationLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationLog
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_IntegrationLog_Id DEFAULT NEWSEQUENTIALID(),
        IntegrationChannelId uniqueidentifier NULL,
        DirectionCode nvarchar(20) NOT NULL,
        OperationCode nvarchar(100) NOT NULL,
        CorrelationId nvarchar(100) NULL,
        IdempotencyKey nvarchar(200) NULL,
        RequestAt datetime2(0) NOT NULL CONSTRAINT DF_IntegrationLog_RequestAt DEFAULT SYSDATETIME(),
        ResponseAt datetime2(0) NULL,
        HttpStatus int NULL,
        LogStatus nvarchar(30) NOT NULL,
        ErrorCode nvarchar(100) NULL,
        ErrorMessage nvarchar(2000) NULL,
        PayloadHash nvarchar(128) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_IntegrationLog_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_IntegrationLog_IsDeleted DEFAULT (0),
        CONSTRAINT PK_IntegrationLog PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
IF OBJECT_ID(N'dbo.LeaveRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveRequest
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_LeaveRequest_Id DEFAULT NEWSEQUENTIALID(),
        RequestNo nvarchar(50) NOT NULL,
        EmployeeId uniqueidentifier NOT NULL,
        FromDate date NOT NULL,
        ToDate date NOT NULL,
        LeaveTypeCode nvarchar(50) NOT NULL,
        Reason nvarchar(1000) NULL,
        RequestStatus nvarchar(30) NOT NULL CONSTRAINT DF_LeaveRequest_Status DEFAULT (N'Pending'),
        ApprovalRequestId uniqueidentifier NULL,
        SubmittedAt datetime2(0) NOT NULL CONSTRAINT DF_LeaveRequest_SubmittedAt DEFAULT SYSDATETIME(),
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_LeaveRequest_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_LeaveRequest_IsDeleted DEFAULT (0),
        CONSTRAINT PK_LeaveRequest PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LeaveRequest_RequestNo UNIQUE (RequestNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.ShiftSwapRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShiftSwapRequest
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_ShiftSwapRequest_Id DEFAULT NEWSEQUENTIALID(),
        RequestNo nvarchar(50) NOT NULL,
        RequesterEmployeeId uniqueidentifier NOT NULL,
        TargetEmployeeId uniqueidentifier NOT NULL,
        CurrentShiftAssignmentId uniqueidentifier NOT NULL,
        ProposedShiftAssignmentId uniqueidentifier NULL,
        Reason nvarchar(1000) NULL,
        RequestStatus nvarchar(30) NOT NULL CONSTRAINT DF_ShiftSwapRequest_Status DEFAULT (N'Pending'),
        ApprovalRequestId uniqueidentifier NULL,
        SubmittedAt datetime2(0) NOT NULL CONSTRAINT DF_ShiftSwapRequest_SubmittedAt DEFAULT SYSDATETIME(),
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_ShiftSwapRequest_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_ShiftSwapRequest_IsDeleted DEFAULT (0),
        CONSTRAINT PK_ShiftSwapRequest PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_ShiftSwapRequest_RequestNo UNIQUE (RequestNo)
    );
END;
GO
/* ==========================================================================
   PHASE 4 - DATA ANALYTICS AND AI-READY KPI FOUNDATION
   ========================================================================== */
IF OBJECT_ID(N'dbo.KpiMetricDefinition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KpiMetricDefinition
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_KpiMetricDefinition_Id DEFAULT NEWSEQUENTIALID(),
        MetricCode nvarchar(100) NOT NULL,
        MetricName nvarchar(255) NOT NULL,
        DomainCode nvarchar(50) NOT NULL,
        UnitCode nvarchar(30) NULL,
        FormulaDescription nvarchar(2000) NOT NULL,
        AggregationCode nvarchar(30) NULL,
        VersionNo int NOT NULL CONSTRAINT DF_KpiMetricDefinition_Version DEFAULT (1),
        EffectiveFrom date NOT NULL,
        EffectiveTo date NULL,
        IsActive bit NOT NULL CONSTRAINT DF_KpiMetricDefinition_Active DEFAULT (1),
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_KpiMetricDefinition_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_KpiMetricDefinition_IsDeleted DEFAULT (0),
        CONSTRAINT PK_KpiMetricDefinition PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_KpiMetricDefinition_Code_Version UNIQUE (MetricCode, VersionNo)
    );
END;
GO
IF OBJECT_ID(N'dbo.KpiDailySnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KpiDailySnapshot
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_KpiDailySnapshot_Id DEFAULT NEWSEQUENTIALID(),
        SnapshotDate date NOT NULL,
        BranchId uniqueidentifier NULL,
        MetricCode nvarchar(100) NOT NULL,
        MetricVersion int NOT NULL,
        MetricValue decimal(28,6) NULL,
        Numerator decimal(28,6) NULL,
        Denominator decimal(28,6) NULL,
        TargetValue decimal(28,6) NULL,
        VarianceValue decimal(28,6) NULL,
        CalculationVersion nvarchar(50) NOT NULL,
        CalculatedAt datetime2(0) NOT NULL CONSTRAINT DF_KpiDailySnapshot_CalculatedAt DEFAULT SYSDATETIME(),
        SourceWatermark nvarchar(500) NULL,
        DataQualityStatus nvarchar(30) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_KpiDailySnapshot_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_KpiDailySnapshot_IsDeleted DEFAULT (0),
        CONSTRAINT PK_KpiDailySnapshot PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_KpiDailySnapshot_Date_Branch_Metric UNIQUE (SnapshotDate, BranchId, MetricCode, MetricVersion)
    );
END;
GO
IF OBJECT_ID(N'dbo.KpiMonthlySnapshot', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KpiMonthlySnapshot
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_KpiMonthlySnapshot_Id DEFAULT NEWSEQUENTIALID(),
        SnapshotMonth date NOT NULL,
        BranchId uniqueidentifier NULL,
        MetricCode nvarchar(100) NOT NULL,
        MetricVersion int NOT NULL,
        MetricValue decimal(28,6) NULL,
        Numerator decimal(28,6) NULL,
        Denominator decimal(28,6) NULL,
        TargetValue decimal(28,6) NULL,
        VarianceValue decimal(28,6) NULL,
        CalculationVersion nvarchar(50) NOT NULL,
        CalculatedAt datetime2(0) NOT NULL CONSTRAINT DF_KpiMonthlySnapshot_CalculatedAt DEFAULT SYSDATETIME(),
        SourceWatermark nvarchar(500) NULL,
        DataQualityStatus nvarchar(30) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_KpiMonthlySnapshot_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_KpiMonthlySnapshot_IsDeleted DEFAULT (0),
        CONSTRAINT PK_KpiMonthlySnapshot PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_KpiMonthlySnapshot_Month_Branch_Metric UNIQUE (SnapshotMonth, BranchId, MetricCode, MetricVersion)
    );
END;
GO
IF OBJECT_ID(N'dbo.AnalyticsRefreshLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AnalyticsRefreshLog
    (
        Id uniqueidentifier NOT NULL CONSTRAINT DF_AnalyticsRefreshLog_Id DEFAULT NEWSEQUENTIALID(),
        JobCode nvarchar(100) NOT NULL,
        RunId uniqueidentifier NOT NULL,
        StartedAt datetime2(0) NOT NULL CONSTRAINT DF_AnalyticsRefreshLog_StartedAt DEFAULT SYSDATETIME(),
        CompletedAt datetime2(0) NULL,
        RunStatus nvarchar(30) NOT NULL,
        RowsProcessed bigint NULL,
        SourceWatermark nvarchar(500) NULL,
        ErrorMessage nvarchar(2000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_AnalyticsRefreshLog_CreatedDate DEFAULT SYSDATETIME(),
        CreatedBy uniqueidentifier NULL,
        ModifiedDate datetime2(0) NULL,
        ModifiedBy uniqueidentifier NULL,
        IsDeleted bit NOT NULL CONSTRAINT DF_AnalyticsRefreshLog_IsDeleted DEFAULT (0),
        CONSTRAINT PK_AnalyticsRefreshLog PRIMARY KEY CLUSTERED (Id)
    );
END;
GO
/* Supporting indexes for common operational lookups. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AttendanceEvent') AND name=N'IX_AttendanceEvent_Employee_EventAt')
    CREATE INDEX IX_AttendanceEvent_Employee_EventAt ON dbo.AttendanceEvent(EmployeeId, EventAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.FoodSafetyChecklistResult') AND name=N'IX_FoodSafetyChecklistResult_Date_Shift')
    CREATE INDEX IX_FoodSafetyChecklistResult_Date_Shift ON dbo.FoodSafetyChecklistResult(CheckDate, ShiftId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.DeliveryEvent') AND name=N'IX_DeliveryEvent_Route_EventAt')
    CREATE INDEX IX_DeliveryEvent_Route_EventAt ON dbo.DeliveryEvent(DeliveryRouteId, EventAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerComplaint') AND name=N'IX_CustomerComplaint_Status_ReceivedAt')
    CREATE INDEX IX_CustomerComplaint_Status_ReceivedAt ON dbo.CustomerComplaint(ComplaintStatus, ReceivedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ApprovalRequest') AND name=N'IX_ApprovalRequest_Status_RequestedAt')
    CREATE INDEX IX_ApprovalRequest_Status_RequestedAt ON dbo.ApprovalRequest(RequestStatus, RequestedAt DESC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.InboundMessage') AND name=N'IX_InboundMessage_Status_ReceivedAt')
    CREATE INDEX IX_InboundMessage_Status_ReceivedAt ON dbo.InboundMessage(ProcessingStatus, ReceivedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.OutboundMessage') AND name=N'IX_OutboundMessage_Status_QueuedAt')
    CREATE INDEX IX_OutboundMessage_Status_QueuedAt ON dbo.OutboundMessage(MessageStatus, QueuedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.IntegrationLog') AND name=N'IX_IntegrationLog_CorrelationId')
    CREATE INDEX IX_IntegrationLog_CorrelationId ON dbo.IntegrationLog(CorrelationId) WHERE CorrelationId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.KpiDailySnapshot') AND name=N'IX_KpiDailySnapshot_Date')
    CREATE INDEX IX_KpiDailySnapshot_Date ON dbo.KpiDailySnapshot(SnapshotDate, MetricCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.KpiMonthlySnapshot') AND name=N'IX_KpiMonthlySnapshot_Month')
    CREATE INDEX IX_KpiMonthlySnapshot_Month ON dbo.KpiMonthlySnapshot(SnapshotMonth, MetricCode);
GO

/* ========================================================================== 
   FINAL VALIDATION
   ========================================================================== */
SELECT s.name AS PhysicalSchema, COUNT_BIG(*) AS PhysicalTableCount
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
GROUP BY s.name ORDER BY s.name;

SELECT name AS NonDboUserSchema
FROM sys.schemas
WHERE name NOT IN (N'dbo', N'guest', N'sys', N'INFORMATION_SCHEMA')
ORDER BY name;

SELECT v.PhaseName, v.TableName, CASE WHEN OBJECT_ID(N'dbo.' + v.TableName, N'U') IS NULL THEN N'MISSING' ELSE N'OK' END AS Status
FROM (VALUES
(N'Phase 2',N'ProductionLine'),(N'Phase 2',N'KitchenWorkstation'),
(N'Phase 2',N'ShiftAssignment'),(N'Phase 2',N'AttendanceEvent'),(N'Phase 2',N'FoodSafetyChecklist'),
(N'Phase 2',N'FoodSafetyChecklistItem'),(N'Phase 2',N'FoodSafetyChecklistResult'),(N'Phase 2',N'TemperatureLog'),(N'Phase 2',N'DeliveryStop'),
(N'Phase 2',N'DeliveryEvent'),(N'Phase 2',N'PackagingLedger'),(N'Phase 2',N'MealWasteRecord'),
(N'Phase 2',N'SupplierIssue'),(N'Phase 2',N'CustomerComplaint'),(N'Phase 2',N'ComplaintHistory'),
(N'Phase 2',N'CleaningChecklistResult'),(N'Phase 2',N'MenuChangeRequest'),
(N'Phase 3',N'ApprovalRequest'),(N'Phase 3',N'ApprovalStep'),(N'Phase 3',N'ApprovalDecision'),(N'Phase 3',N'ApprovalItem'),
(N'Phase 3',N'IntegrationChannel'),(N'Phase 3',N'BotConfiguration'),(N'Phase 3',N'UserExternalIdentity'),
(N'Phase 3',N'InboundMessage'),(N'Phase 3',N'OutboundMessage'),(N'Phase 3',N'IntegrationLog'),
(N'Phase 3',N'LeaveRequest'),(N'Phase 3',N'ShiftSwapRequest'),
(N'Phase 4',N'KpiMetricDefinition'),(N'Phase 4',N'KpiDailySnapshot'),(N'Phase 4',N'KpiMonthlySnapshot'),(N'Phase 4',N'AnalyticsRefreshLog')
) v(PhaseName,TableName)
ORDER BY v.PhaseName, v.TableName;
GO

/* IMPORTANT: KPI snapshot tables are empty by design until metric formulas,
   branch scope, data source mapping and refresh cadence are approved. */
PRINT N'DELICARE: All four database phases have been processed. Review Phase 1 conflict records and validation output before application release.';
GO


/* ==========================================================================
   FINAL REVIEW - DEPRECATION CANDIDATES AND DATABASE INVENTORY
   ========================================================================== */
/* These objects appear clinic/demo-oriented in the supplied actual schema.
   They are candidates for deprecation only after checking .NET/EF/API usage,
   data retention and any external integrations. This script does not drop them. */
SELECT t.name AS CandidateForDeprecation
FROM sys.tables t
WHERE t.schema_id = SCHEMA_ID(N'dbo')
  AND t.name IN
  (
      N'Patient', N'PatientDetail', N'MedicalExamination',
      N'MedicineWarehouse', N'TestDemo'
  )
ORDER BY t.name;
GO

/* Person is retained because dbo.User.PersonId references it.
   Administrative geography tables are retained pending address/controller
   dependency review. DocumentFile is retained pending attachment review. */

/* Final physical table inventory: all business tables remain under dbo. */
SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'dbo'
ORDER BY t.name;
GO

/* Confirm that no user-defined physical schema other than dbo was introduced. */
SELECT name AS UserSchema
FROM sys.schemas
WHERE name NOT IN (N'dbo', N'guest', N'sys', N'INFORMATION_SCHEMA')
ORDER BY name;
GO
