USE [AppDelicare]
GO
/****** Object:  Table [dbo].[ActualCost]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ActualCost](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionBatchId] [uniqueidentifier] NOT NULL,
	[MaterialCost] [decimal](18, 2) NULL,
	[LaborCost] [decimal](18, 2) NULL,
	[OverheadCost] [decimal](18, 2) NULL,
	[WasteCost] [decimal](18, 2) NULL,
	[TotalCost] [decimal](18, 2) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ProductionOrderId] [uniqueidentifier] NULL,
	[OtherCost] [decimal](18, 2) NOT NULL,
	[CurrencyCode] [nvarchar](10) NULL,
	[CalculatedAt] [datetime2](7) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ActualCost] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ActualCosts_Legacy_20261113]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ActualCosts_Legacy_20261113](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionBatchId] [uniqueidentifier] NOT NULL,
	[MaterialCost] [decimal](18, 2) NULL,
	[LaborCost] [decimal](18, 2) NULL,
	[OverheadCost] [decimal](18, 2) NULL,
	[WasteCost] [decimal](18, 2) NULL,
	[TotalCost] [decimal](18, 2) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ProductionOrderId] [uniqueidentifier] NULL,
	[OtherCost] [decimal](18, 2) NOT NULL,
	[CurrencyCode] [nvarchar](10) NULL,
	[CalculatedAt] [datetime2](7) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ActualCosts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdministrativeDistrict]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdministrativeDistrict](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](max) NULL,
	[CodeName] [nvarchar](max) NULL,
	[NameVN] [nvarchar](max) NULL,
	[NameEN] [nvarchar](max) NULL,
	[FullNameVN] [nvarchar](max) NULL,
	[FullNameEN] [nvarchar](max) NULL,
	[AdministrativeProvinceId] [uniqueidentifier] NULL,
	[AdministrativeUnitId] [uniqueidentifier] NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdministrativeDistrict] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdministrativeProvince]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdministrativeProvince](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](max) NULL,
	[CodeName] [nvarchar](max) NULL,
	[NameVN] [nvarchar](max) NULL,
	[NameEN] [nvarchar](max) NULL,
	[FullNameVN] [nvarchar](max) NULL,
	[FullNameEN] [nvarchar](max) NULL,
	[AdministrativeUnitId] [uniqueidentifier] NULL,
	[AdministrativeRegionId] [uniqueidentifier] NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdministrativeProvince] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdministrativeRegion]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdministrativeRegion](
	[Id] [uniqueidentifier] NOT NULL,
	[CodeVN] [nvarchar](max) NULL,
	[CodeEN] [nvarchar](max) NULL,
	[NameVN] [nvarchar](max) NULL,
	[NameEN] [nvarchar](max) NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdministrativeRegion] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdministrativeUnit]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdministrativeUnit](
	[Id] [uniqueidentifier] NOT NULL,
	[CodeNameVN] [nvarchar](max) NULL,
	[CodeNameEN] [nvarchar](max) NULL,
	[FullNameVN] [nvarchar](max) NULL,
	[FullNameEN] [nvarchar](max) NULL,
	[ShortNameVN] [nvarchar](max) NULL,
	[ShortNameEN] [nvarchar](max) NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdministrativeUnit] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AdministrativeWard]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AdministrativeWard](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](max) NULL,
	[CodeName] [nvarchar](max) NULL,
	[NameVN] [nvarchar](max) NULL,
	[NameEN] [nvarchar](max) NULL,
	[FullNameVN] [nvarchar](max) NULL,
	[FullNameEN] [nvarchar](max) NULL,
	[AdministrativeDistrictId] [uniqueidentifier] NULL,
	[AdministrativeUnitId] [uniqueidentifier] NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AdministrativeWard] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AnalyticsRefreshLog]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AnalyticsRefreshLog](
	[Id] [uniqueidentifier] NOT NULL,
	[JobCode] [nvarchar](100) NOT NULL,
	[RunId] [uniqueidentifier] NOT NULL,
	[StartedAt] [datetime2](0) NOT NULL,
	[CompletedAt] [datetime2](0) NULL,
	[RunStatus] [nvarchar](30) NOT NULL,
	[RowsProcessed] [bigint] NULL,
	[SourceWatermark] [nvarchar](500) NULL,
	[ErrorMessage] [nvarchar](2000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AnalyticsRefreshLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ApprovalDecision]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApprovalDecision](
	[Id] [uniqueidentifier] NOT NULL,
	[ApprovalRequestId] [uniqueidentifier] NOT NULL,
	[ApprovalStepId] [uniqueidentifier] NOT NULL,
	[DecisionCode] [nvarchar](30) NOT NULL,
	[DecidedBy] [uniqueidentifier] NOT NULL,
	[DecidedAt] [datetime2](0) NOT NULL,
	[Comment] [nvarchar](2000) NULL,
	[PayloadHash] [nvarchar](128) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ApprovalDecision] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ApprovalItem]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApprovalItem](
	[Id] [uniqueidentifier] NOT NULL,
	[ApprovalRequestId] [uniqueidentifier] NOT NULL,
	[ItemType] [nvarchar](100) NOT NULL,
	[ItemId] [uniqueidentifier] NULL,
	[ActionCode] [nvarchar](50) NOT NULL,
	[PayloadJson] [nvarchar](max) NULL,
	[ItemStatus] [nvarchar](30) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ApprovalItem] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ApprovalRequest]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApprovalRequest](
	[Id] [uniqueidentifier] NOT NULL,
	[RequestNo] [nvarchar](50) NOT NULL,
	[BusinessType] [nvarchar](100) NOT NULL,
	[BusinessId] [uniqueidentifier] NULL,
	[RequestedBy] [uniqueidentifier] NULL,
	[RequestedAt] [datetime2](0) NOT NULL,
	[RequestStatus] [nvarchar](30) NOT NULL,
	[CurrentStepNo] [int] NULL,
	[PriorityCode] [nvarchar](30) NULL,
	[DueAt] [datetime2](0) NULL,
	[PayloadJson] [nvarchar](max) NULL,
	[CorrelationId] [nvarchar](100) NULL,
	[CompletedAt] [datetime2](0) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ApprovalRequest] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ApprovalRequest_RequestNo] UNIQUE NONCLUSTERED 
(
	[RequestNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ApprovalStep]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ApprovalStep](
	[Id] [uniqueidentifier] NOT NULL,
	[ApprovalRequestId] [uniqueidentifier] NOT NULL,
	[StepNo] [int] NOT NULL,
	[ApproverUserId] [uniqueidentifier] NULL,
	[ApproverRoleCode] [nvarchar](100) NULL,
	[DecisionStatus] [nvarchar](30) NOT NULL,
	[AssignedAt] [datetime2](0) NULL,
	[DueAt] [datetime2](0) NULL,
	[DecidedAt] [datetime2](0) NULL,
	[DecisionNote] [nvarchar](2000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ApprovalStep] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ApprovalStep_Request_Step] UNIQUE NONCLUSTERED 
(
	[ApprovalRequestId] ASC,
	[StepNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AttendanceEvent]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AttendanceEvent](
	[Id] [uniqueidentifier] NOT NULL,
	[EmployeeId] [uniqueidentifier] NOT NULL,
	[ShiftAssignmentId] [uniqueidentifier] NULL,
	[EventType] [nvarchar](30) NOT NULL,
	[EventAt] [datetime2](0) NOT NULL,
	[Latitude] [decimal](10, 7) NULL,
	[Longitude] [decimal](10, 7) NULL,
	[DistanceMeters] [decimal](10, 2) NULL,
	[IsValid] [bit] NULL,
	[SourceChannel] [nvarchar](50) NULL,
	[ExternalEventId] [nvarchar](200) NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AttendanceEvent] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AuditChange]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AuditChange](
	[Id] [uniqueidentifier] NOT NULL,
	[IdChange] [uniqueidentifier] NULL,
	[Action] [nvarchar](max) NULL,
	[EntityName] [nvarchar](max) NULL,
	[Changes] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_AuditChange] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BotConfiguration]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BotConfiguration](
	[Id] [uniqueidentifier] NOT NULL,
	[IntegrationChannelId] [uniqueidentifier] NOT NULL,
	[BotCode] [nvarchar](50) NOT NULL,
	[BotName] [nvarchar](255) NOT NULL,
	[BotUsername] [nvarchar](255) NULL,
	[TargetAudience] [nvarchar](100) NULL,
	[WebhookPath] [nvarchar](500) NULL,
	[BotStatus] [nvarchar](30) NOT NULL,
	[LastTokenRotatedAt] [datetime2](0) NULL,
	[SecretReference] [nvarchar](500) NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_BotConfiguration] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_BotConfiguration_BotCode] UNIQUE NONCLUSTERED 
(
	[BotCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CleaningBatche]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CleaningBatche](
	[Id] [uniqueidentifier] NOT NULL,
	[CleaningNo] [nvarchar](255) NULL,
	[ReturnInspectionId] [uniqueidentifier] NOT NULL,
	[StartTime] [datetime] NULL,
	[EndTime] [datetime] NULL,
	[Quantity] [decimal](18, 6) NULL,
	[QcResult] [nvarchar](50) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CleaningBatchNumber] [nvarchar](max) NULL,
	[StartDate] [datetime2](7) NOT NULL,
	[EndDate] [datetime2](7) NULL,
	[ContainerQuantity] [int] NOT NULL,
	[ResponsibleEmployeeId] [uniqueidentifier] NULL,
	[QualityStatus] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_CleaningBatches] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CleaningChecklistResult]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CleaningChecklistResult](
	[Id] [uniqueidentifier] NOT NULL,
	[CleaningBatchId] [uniqueidentifier] NULL,
	[WorkstationId] [uniqueidentifier] NULL,
	[ChecklistId] [uniqueidentifier] NULL,
	[ChecklistItemId] [uniqueidentifier] NULL,
	[ResultCode] [nvarchar](30) NOT NULL,
	[ActualValue] [nvarchar](500) NULL,
	[CheckedAt] [datetime2](0) NOT NULL,
	[CheckedBy] [uniqueidentifier] NULL,
	[ChemicalUsed] [nvarchar](255) NULL,
	[CorrectiveAction] [nvarchar](2000) NULL,
	[EvidenceUrl] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_CleaningChecklistResult] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Company]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Company](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[TaxCode] [nvarchar](100) NULL,
	[Address] [nvarchar](max) NULL,
	[Phone] [nvarchar](100) NULL,
	[Email] [nvarchar](255) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Companies] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ComplaintHistory]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ComplaintHistory](
	[Id] [uniqueidentifier] NOT NULL,
	[ComplaintId] [uniqueidentifier] NOT NULL,
	[EventType] [nvarchar](50) NOT NULL,
	[OldStatus] [nvarchar](30) NULL,
	[NewStatus] [nvarchar](30) NULL,
	[Comment] [nvarchar](2000) NULL,
	[EventAt] [datetime2](0) NOT NULL,
	[ActorId] [uniqueidentifier] NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ComplaintHistory] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContainerReturn]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContainerReturn](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryOrderId] [uniqueidentifier] NOT NULL,
	[ReturnDate] [datetime] NOT NULL,
	[Quantity] [decimal](18, 6) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CustomerId] [uniqueidentifier] NULL,
	[ExpectedQuantity] [int] NOT NULL,
	[ReturnedQuantity] [int] NOT NULL,
	[DamagedQuantity] [int] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ContainerReturns] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Contract]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Contract](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[ContractNo] [nvarchar](255) NULL,
	[StartDate] [datetime] NULL,
	[EndDate] [datetime] NULL,
	[PaymentTerm] [nvarchar](max) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ContractNumber] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[SignedDate] [datetime2](7) NULL,
	[EffectiveFrom] [datetime2](7) NOT NULL,
	[EffectiveTo] [datetime2](7) NULL,
	[ContractValue] [decimal](18, 4) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Contracts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ContractMeal]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ContractMeal](
	[Id] [uniqueidentifier] NOT NULL,
	[ContractId] [uniqueidentifier] NOT NULL,
	[MealType] [nvarchar](100) NULL,
	[ServiceType] [nvarchar](100) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MealName] [nvarchar](max) NULL,
	[UnitPrice] [decimal](18, 4) NULL,
	[UnitId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ContractMeals] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CoreDataMergeConflict]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CoreDataMergeConflict](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[MigrationCode] [nvarchar](50) NOT NULL,
	[EntityName] [sysname] NOT NULL,
	[RecordId] [uniqueidentifier] NULL,
	[FieldName] [sysname] NOT NULL,
	[CanonicalValue] [nvarchar](max) NULL,
	[LegacyValue] [nvarchar](max) NULL,
	[DetectedAt] [datetime2](7) NOT NULL,
	[ResolvedAt] [datetime2](7) NULL,
	[ResolutionNote] [nvarchar](1000) NULL,
 CONSTRAINT [PK_CoreDataMergeConflict] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Customer]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Customer](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[CustomerType] [nvarchar](20) NOT NULL,
	[Status] [nvarchar](50) NOT NULL,
	[CIF] [nvarchar](50) NOT NULL,
	[DisplayName] [nvarchar](255) NOT NULL,
	[PhoneNumber] [nvarchar](50) NULL,
	[Email] [nvarchar](255) NULL,
	[Address] [nvarchar](500) NULL,
	[TaxCode] [nvarchar](100) NULL,
	[FirstName] [nvarchar](100) NULL,
	[LastName] [nvarchar](100) NULL,
	[Gender] [nvarchar](20) NULL,
	[DateOfBirth] [datetime] NULL,
	[IdentityNumber] [nvarchar](50) NULL,
	[IdentityDate] [datetime] NULL,
	[IdentityPlace] [nvarchar](255) NULL,
	[Occupation] [nvarchar](100) NULL,
	[LegalName] [nvarchar](255) NULL,
	[BusinessRegistrationNumber] [nvarchar](50) NULL,
	[RegistrationDate] [datetime] NULL,
	[RegistrationPlace] [nvarchar](255) NULL,
	[CompanySize] [nvarchar](50) NULL,
	[Industry] [nvarchar](100) NULL,
	[LegalRepresentativeName] [nvarchar](255) NULL,
	[ContactPersonName] [nvarchar](255) NULL,
	[ContactPersonPhone] [nvarchar](50) NULL,
	[ContactPersonEmail] [nvarchar](255) NULL,
	[Segment] [nvarchar](50) NULL,
	[Source] [nvarchar](100) NULL,
	[RegionId] [uniqueidentifier] NULL,
	[AreaId] [uniqueidentifier] NULL,
	[BranchId] [uniqueidentifier] NULL,
	[DepartmentId] [uniqueidentifier] NULL,
	[UserAssignId] [uniqueidentifier] NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[Note] [nvarchar](max) NULL,
	[ExternalData] [nvarchar](max) NULL,
	[ExternalValue] [nvarchar](500) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Customer] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CustomerComplaint]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CustomerComplaint](
	[Id] [uniqueidentifier] NOT NULL,
	[ComplaintNo] [nvarchar](50) NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[OrderId] [uniqueidentifier] NULL,
	[DeliveryOrderId] [uniqueidentifier] NULL,
	[DeliveryRouteId] [uniqueidentifier] NULL,
	[SourceChannel] [nvarchar](50) NULL,
	[CategoryCode] [nvarchar](50) NULL,
	[Severity] [nvarchar](30) NOT NULL,
	[Description] [nvarchar](2000) NOT NULL,
	[ReceivedAt] [datetime2](0) NOT NULL,
	[SlaDueAt] [datetime2](0) NULL,
	[ComplaintStatus] [nvarchar](30) NOT NULL,
	[AssignedTo] [uniqueidentifier] NULL,
	[ResolvedAt] [datetime2](0) NULL,
	[Resolution] [nvarchar](2000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_CustomerComplaint] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_CustomerComplaint_ComplaintNo] UNIQUE NONCLUSTERED 
(
	[ComplaintNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CustomerContact]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CustomerContact](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](500) NULL,
	[Position] [nvarchar](255) NULL,
	[Phone] [nvarchar](100) NULL,
	[Email] [nvarchar](255) NULL,
	[IsPrimary] [bit] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[FullName] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_CustomerContacts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CustomerDeliveryLocation]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CustomerDeliveryLocation](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](500) NULL,
	[Address] [nvarchar](max) NULL,
	[ContactName] [nvarchar](500) NULL,
	[ContactPhone] [nvarchar](100) NULL,
	[Latitude] [decimal](18, 10) NULL,
	[Longitude] [decimal](18, 10) NULL,
	[DeliveryWindowFrom] [time](7) NULL,
	[DeliveryWindowTo] [time](7) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeliveryInstructions] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_CustomerDeliveryLocations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DeliveryEvent]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeliveryEvent](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryRouteId] [uniqueidentifier] NOT NULL,
	[DeliveryStopId] [uniqueidentifier] NULL,
	[DeliveryOrderId] [uniqueidentifier] NULL,
	[EventType] [nvarchar](50) NOT NULL,
	[EventAt] [datetime2](0) NOT NULL,
	[Latitude] [decimal](10, 7) NULL,
	[Longitude] [decimal](10, 7) NULL,
	[TemperatureC] [decimal](6, 2) NULL,
	[PhotoUrl] [nvarchar](1000) NULL,
	[Description] [nvarchar](2000) NULL,
	[RecordedBy] [uniqueidentifier] NULL,
	[SourceChannel] [nvarchar](50) NULL,
	[ExternalEventId] [nvarchar](200) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_DeliveryEvent] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DeliveryExecution]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeliveryExecution](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryOrderId] [uniqueidentifier] NOT NULL,
	[DeliveryRouteId] [uniqueidentifier] NULL,
	[Status] [nvarchar](50) NULL,
	[DispatchedAt] [datetime] NULL,
	[ArrivedAt] [datetime] NULL,
	[DeliveredAt] [datetime] NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DepartedAt] [datetime2](7) NULL,
	[DriverName] [nvarchar](max) NULL,
	[VehicleCode] [nvarchar](max) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_DeliveryExecutions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DeliveryLoading]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeliveryLoading](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryRouteId] [uniqueidentifier] NOT NULL,
	[DeliveryOrderId] [uniqueidentifier] NOT NULL,
	[LoadedQty] [decimal](18, 6) NULL,
	[LoadingTime] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[EmployeeId] [uniqueidentifier] NULL,
	[VehicleCode] [nvarchar](max) NULL,
	[LoadingDate] [datetime2](7) NOT NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_DeliveryLoadings] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DeliveryOrder]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeliveryOrder](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryNo] [nvarchar](255) NULL,
	[OrderId] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[DeliveryLocationId] [uniqueidentifier] NULL,
	[DeliveryDate] [datetime] NOT NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CustomerDeliveryLocationId] [uniqueidentifier] NULL,
	[DeliveryRouteId] [uniqueidentifier] NULL,
	[DeliveryNumber] [nvarchar](max) NULL,
	[PlannedDeliveryTime] [datetime2](7) NOT NULL,
	[ActualDeliveryTime] [datetime2](7) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_DeliveryOrders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DeliveryRoute]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeliveryRoute](
	[Id] [uniqueidentifier] NOT NULL,
	[RouteNo] [nvarchar](255) NULL,
	[DeliveryDate] [datetime] NOT NULL,
	[VehicleId] [uniqueidentifier] NULL,
	[DriverId] [uniqueidentifier] NULL,
	[PlannedStart] [datetime] NULL,
	[PlannedEnd] [datetime] NULL,
	[ActualStart] [datetime] NULL,
	[ActualEnd] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[Code] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[Description] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_DeliveryRoutes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DeliveryStop]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DeliveryStop](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryRouteId] [uniqueidentifier] NOT NULL,
	[DeliveryOrderId] [uniqueidentifier] NULL,
	[CustomerId] [uniqueidentifier] NULL,
	[DeliveryLocationId] [uniqueidentifier] NULL,
	[SequenceNo] [int] NOT NULL,
	[PlannedArrival] [datetime2](0) NULL,
	[ActualArrival] [datetime2](0) NULL,
	[PlannedMealQty] [decimal](18, 3) NULL,
	[DeliveredMealQty] [decimal](18, 3) NULL,
	[ShortageQty] [decimal](18, 3) NULL,
	[StopStatus] [nvarchar](30) NOT NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_DeliveryStop] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_DeliveryStop_Route_Sequence] UNIQUE NONCLUSTERED 
(
	[DeliveryRouteId] ASC,
	[SequenceNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DishCategorie]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DishCategorie](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ParentId] [uniqueidentifier] NULL,
	[Description] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_DishCategories] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Dishe]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Dishe](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[CategoryId] [uniqueidentifier] NULL,
	[MealCategory] [nvarchar](100) NULL,
	[Description] [nvarchar](max) NULL,
	[StandardPortion] [decimal](18, 6) NULL,
	[StandardCost] [decimal](18, 2) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DishCategoryId] [uniqueidentifier] NULL,
	[DefaultUnitId] [uniqueidentifier] NULL,
	[StandardServingSize] [decimal](18, 4) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Dishes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DocumentFile]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentFile](
	[Id] [uniqueidentifier] NOT NULL,
	[TestDemoId] [uniqueidentifier] NULL,
	[FileName] [nvarchar](max) NULL,
	[FileSize] [int] NULL,
	[FileType] [nvarchar](max) NULL,
	[FileNameHas] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_DocumentFile] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Employee]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Employee](
	[Id] [uniqueidentifier] NOT NULL,
	[EmployeeCode] [nvarchar](255) NULL,
	[FullName] [nvarchar](500) NULL,
	[OrganizationUnitId] [uniqueidentifier] NULL,
	[PositionId] [uniqueidentifier] NULL,
	[Phone] [nvarchar](100) NULL,
	[Email] [nvarchar](255) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[Code] [nvarchar](max) NULL,
	[DateOfBirth] [datetime2](7) NULL,
	[Gender] [nvarchar](max) NULL,
	[Address] [nvarchar](max) NULL,
	[HireDate] [datetime2](7) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Employees] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FoodSafetyChecklist]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FoodSafetyChecklist](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[VersionNo] [int] NOT NULL,
	[AreaCode] [nvarchar](50) NULL,
	[FrequencyCode] [nvarchar](30) NULL,
	[EffectiveFrom] [date] NULL,
	[EffectiveTo] [date] NULL,
	[Status] [nvarchar](30) NOT NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_FoodSafetyChecklist] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_FoodSafetyChecklist_Code_Version] UNIQUE NONCLUSTERED 
(
	[Code] ASC,
	[VersionNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FoodSafetyChecklistItem]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FoodSafetyChecklistItem](
	[Id] [uniqueidentifier] NOT NULL,
	[ChecklistId] [uniqueidentifier] NOT NULL,
	[ItemCode] [nvarchar](50) NOT NULL,
	[ItemName] [nvarchar](500) NOT NULL,
	[Description] [nvarchar](1000) NULL,
	[SortOrder] [int] NOT NULL,
	[IsCritical] [bit] NOT NULL,
	[ExpectedValue] [nvarchar](500) NULL,
	[UnitCode] [nvarchar](30) NULL,
	[IsRequired] [bit] NOT NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_FoodSafetyChecklistItem] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_FoodSafetyChecklistItem_Checklist_Item] UNIQUE NONCLUSTERED 
(
	[ChecklistId] ASC,
	[ItemCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FoodSafetyChecklistResult]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FoodSafetyChecklistResult](
	[Id] [uniqueidentifier] NOT NULL,
	[ChecklistId] [uniqueidentifier] NOT NULL,
	[ChecklistItemId] [uniqueidentifier] NOT NULL,
	[CheckDate] [date] NOT NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[ProductionBatchId] [uniqueidentifier] NULL,
	[WorkstationId] [uniqueidentifier] NULL,
	[ResultCode] [nvarchar](30) NOT NULL,
	[ActualValue] [nvarchar](500) NULL,
	[CheckedBy] [uniqueidentifier] NULL,
	[CheckedAt] [datetime2](0) NOT NULL,
	[CorrectiveAction] [nvarchar](2000) NULL,
	[EvidenceUrl] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_FoodSafetyChecklistResult] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Forecast]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Forecast](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[DeliveryLocationId] [uniqueidentifier] NULL,
	[ForecastDate] [datetime] NOT NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[MealType] [nvarchar](100) NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[VersionNo] [int] NOT NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ForecastQuantity] [decimal](18, 4) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Forecasts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GoodsReceipt]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GoodsReceipt](
	[Id] [uniqueidentifier] NOT NULL,
	[ReceiptNo] [nvarchar](255) NULL,
	[PurchaseOrderId] [uniqueidentifier] NULL,
	[SupplierId] [uniqueidentifier] NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[ReceiptDate] [datetime] NOT NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ReceiptNumber] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_GoodsReceipts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[GoodsReceiptLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[GoodsReceiptLine](
	[Id] [uniqueidentifier] NOT NULL,
	[GoodsReceiptId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UomId] [uniqueidentifier] NOT NULL,
	[UnitCost] [decimal](18, 2) NULL,
	[LotId] [uniqueidentifier] NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ReceivedQuantity] [decimal](18, 4) NOT NULL,
	[AcceptedQuantity] [decimal](18, 4) NOT NULL,
	[RejectedQuantity] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[LotNumber] [nvarchar](max) NULL,
	[ManufactureDate] [datetime2](7) NULL,
	[ExpiryDate] [datetime2](7) NULL,
	[QualityStatus] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_GoodsReceiptLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InboundMessage]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InboundMessage](
	[Id] [uniqueidentifier] NOT NULL,
	[IntegrationChannelId] [uniqueidentifier] NOT NULL,
	[ExternalMessageId] [nvarchar](255) NULL,
	[ExternalConversationId] [nvarchar](255) NULL,
	[ExternalUserId] [nvarchar](255) NULL,
	[MessageType] [nvarchar](50) NULL,
	[PayloadJson] [nvarchar](max) NULL,
	[ReceivedAt] [datetime2](0) NOT NULL,
	[ProcessingStatus] [nvarchar](30) NOT NULL,
	[IdempotencyKey] [nvarchar](200) NULL,
	[CorrelationId] [nvarchar](100) NULL,
	[ErrorMessage] [nvarchar](2000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_InboundMessage] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Ingredient]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Ingredient](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[CategoryId] [uniqueidentifier] NULL,
	[BaseUomId] [uniqueidentifier] NULL,
	[PurchaseUomId] [uniqueidentifier] NULL,
	[IssueUomId] [uniqueidentifier] NULL,
	[StorageType] [nvarchar](100) NULL,
	[ShelfLifeDays] [int] NULL,
	[YieldPercent] [decimal](18, 6) NULL,
	[StandardWastePercent] [decimal](18, 6) NULL,
	[LotControl] [bit] NULL,
	[ExpiryControl] [bit] NULL,
	[QcRequired] [bit] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[IngredientCategoryId] [uniqueidentifier] NULL,
	[BaseUnitId] [uniqueidentifier] NULL,
	[Specification] [nvarchar](max) NULL,
	[StorageCondition] [nvarchar](max) NULL,
	[IsExpiryManaged] [bit] NOT NULL,
	[MinimumStock] [decimal](18, 4) NULL,
	[SafetyStock] [decimal](18, 4) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Ingredients] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IngredientCategorie]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IngredientCategorie](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[ParentId] [uniqueidentifier] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[Description] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_IngredientCategories] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IntegrationChannel]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IntegrationChannel](
	[Id] [uniqueidentifier] NOT NULL,
	[ChannelCode] [nvarchar](50) NOT NULL,
	[ChannelName] [nvarchar](255) NOT NULL,
	[ProviderCode] [nvarchar](50) NOT NULL,
	[EnvironmentCode] [nvarchar](30) NOT NULL,
	[ChannelStatus] [nvarchar](30) NOT NULL,
	[ConfigJson] [nvarchar](max) NULL,
	[SecretReference] [nvarchar](500) NULL,
	[OwnerUserId] [uniqueidentifier] NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_IntegrationChannel] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_IntegrationChannel_Code] UNIQUE NONCLUSTERED 
(
	[ChannelCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[IntegrationLog]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IntegrationLog](
	[Id] [uniqueidentifier] NOT NULL,
	[IntegrationChannelId] [uniqueidentifier] NULL,
	[DirectionCode] [nvarchar](20) NOT NULL,
	[OperationCode] [nvarchar](100) NOT NULL,
	[CorrelationId] [nvarchar](100) NULL,
	[IdempotencyKey] [nvarchar](200) NULL,
	[RequestAt] [datetime2](0) NOT NULL,
	[ResponseAt] [datetime2](0) NULL,
	[HttpStatus] [int] NULL,
	[LogStatus] [nvarchar](30) NOT NULL,
	[ErrorCode] [nvarchar](100) NULL,
	[ErrorMessage] [nvarchar](2000) NULL,
	[PayloadHash] [nvarchar](128) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_IntegrationLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InventoryBalance]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InventoryBalance](
	[Id] [uniqueidentifier] NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[StorageLocationId] [uniqueidentifier] NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[LotId] [uniqueidentifier] NULL,
	[OnHandQty] [decimal](18, 6) NOT NULL,
	[ReservedQty] [decimal](18, 6) NOT NULL,
	[AvailableQty] [decimal](18, 6) NOT NULL,
	[UpdatedDate] [datetime] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MaterialLotId] [uniqueidentifier] NULL,
	[OnHandQuantity] [decimal](18, 4) NOT NULL,
	[ReservedQuantity] [decimal](18, 4) NOT NULL,
	[AvailableQuantity] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[LastCalculatedDate] [datetime2](7) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_InventoryBalances] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InventoryPolicie]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InventoryPolicie](
	[Id] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[MinStock] [decimal](18, 6) NULL,
	[MaxStock] [decimal](18, 6) NULL,
	[SafetyStock] [decimal](18, 6) NULL,
	[ReorderPoint] [decimal](18, 6) NULL,
	[CriticalLevel] [decimal](18, 6) NULL,
	[LeadTimeDays] [int] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MinimumStock] [decimal](18, 4) NULL,
	[MaximumStock] [decimal](18, 4) NULL,
	[UseFefo] [bit] NOT NULL,
	[ExpiryWarningDays] [int] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_InventoryPolicies] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InventoryTransaction]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InventoryTransaction](
	[Id] [uniqueidentifier] NOT NULL,
	[TransactionNo] [nvarchar](255) NULL,
	[TransactionType] [nvarchar](50) NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[StorageLocationId] [uniqueidentifier] NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[LotId] [uniqueidentifier] NULL,
	[ReferenceType] [nvarchar](100) NULL,
	[ReferenceId] [uniqueidentifier] NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UomId] [uniqueidentifier] NOT NULL,
	[UnitCost] [decimal](18, 2) NULL,
	[TransactionDate] [datetime] NOT NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MaterialLotId] [uniqueidentifier] NULL,
	[TransactionNumber] [nvarchar](max) NULL,
	[QuantityIn] [decimal](18, 4) NOT NULL,
	[QuantityOut] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[Reason] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[KitchenWorkstation]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KitchenWorkstation](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[AreaName] [nvarchar](255) NULL,
	[Latitude] [decimal](10, 7) NULL,
	[Longitude] [decimal](10, 7) NULL,
	[RadiusMeters] [decimal](10, 2) NULL,
	[Status] [nvarchar](30) NOT NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[OrganizationUnitId] [uniqueidentifier] NULL,
 CONSTRAINT [PK_KitchenWorkstation] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_KitchenWorkstation_Code] UNIQUE NONCLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[KpiDailySnapshot]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KpiDailySnapshot](
	[Id] [uniqueidentifier] NOT NULL,
	[SnapshotDate] [date] NOT NULL,
	[BranchId] [uniqueidentifier] NULL,
	[MetricCode] [nvarchar](100) NOT NULL,
	[MetricVersion] [int] NOT NULL,
	[MetricValue] [decimal](28, 6) NULL,
	[Numerator] [decimal](28, 6) NULL,
	[Denominator] [decimal](28, 6) NULL,
	[TargetValue] [decimal](28, 6) NULL,
	[VarianceValue] [decimal](28, 6) NULL,
	[CalculationVersion] [nvarchar](50) NOT NULL,
	[CalculatedAt] [datetime2](0) NOT NULL,
	[SourceWatermark] [nvarchar](500) NULL,
	[DataQualityStatus] [nvarchar](30) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_KpiDailySnapshot] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_KpiDailySnapshot_Date_Branch_Metric] UNIQUE NONCLUSTERED 
(
	[SnapshotDate] ASC,
	[BranchId] ASC,
	[MetricCode] ASC,
	[MetricVersion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[KpiMetricDefinition]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KpiMetricDefinition](
	[Id] [uniqueidentifier] NOT NULL,
	[MetricCode] [nvarchar](100) NOT NULL,
	[MetricName] [nvarchar](255) NOT NULL,
	[DomainCode] [nvarchar](50) NOT NULL,
	[UnitCode] [nvarchar](30) NULL,
	[FormulaDescription] [nvarchar](2000) NOT NULL,
	[AggregationCode] [nvarchar](30) NULL,
	[VersionNo] [int] NOT NULL,
	[EffectiveFrom] [date] NOT NULL,
	[EffectiveTo] [date] NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_KpiMetricDefinition] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_KpiMetricDefinition_Code_Version] UNIQUE NONCLUSTERED 
(
	[MetricCode] ASC,
	[VersionNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[KpiMonthlySnapshot]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[KpiMonthlySnapshot](
	[Id] [uniqueidentifier] NOT NULL,
	[SnapshotMonth] [date] NOT NULL,
	[BranchId] [uniqueidentifier] NULL,
	[MetricCode] [nvarchar](100) NOT NULL,
	[MetricVersion] [int] NOT NULL,
	[MetricValue] [decimal](28, 6) NULL,
	[Numerator] [decimal](28, 6) NULL,
	[Denominator] [decimal](28, 6) NULL,
	[TargetValue] [decimal](28, 6) NULL,
	[VarianceValue] [decimal](28, 6) NULL,
	[CalculationVersion] [nvarchar](50) NOT NULL,
	[CalculatedAt] [datetime2](0) NOT NULL,
	[SourceWatermark] [nvarchar](500) NULL,
	[DataQualityStatus] [nvarchar](30) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_KpiMonthlySnapshot] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_KpiMonthlySnapshot_Month_Branch_Metric] UNIQUE NONCLUSTERED 
(
	[SnapshotMonth] ASC,
	[BranchId] ASC,
	[MetricCode] ASC,
	[MetricVersion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LeaveRequest]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LeaveRequest](
	[Id] [uniqueidentifier] NOT NULL,
	[RequestNo] [nvarchar](50) NOT NULL,
	[EmployeeId] [uniqueidentifier] NOT NULL,
	[FromDate] [date] NOT NULL,
	[ToDate] [date] NOT NULL,
	[LeaveTypeCode] [nvarchar](50) NOT NULL,
	[Reason] [nvarchar](1000) NULL,
	[RequestStatus] [nvarchar](30) NOT NULL,
	[ApprovalRequestId] [uniqueidentifier] NULL,
	[SubmittedAt] [datetime2](0) NOT NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_LeaveRequest] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_LeaveRequest_RequestNo] UNIQUE NONCLUSTERED 
(
	[RequestNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LocationTracking]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LocationTracking](
	[Id] [uniqueidentifier] NOT NULL,
	[IPv4] [nvarchar](max) NULL,
	[PcName] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_LocationTracking] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MaterialConsumptionVariance]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MaterialConsumptionVariance](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionOrderId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[TheoreticalQty] [decimal](18, 6) NULL,
	[ActualQty] [decimal](18, 6) NULL,
	[VarianceQty] [decimal](18, 6) NULL,
	[VariancePercent] [decimal](18, 6) NULL,
	[VarianceValue] [decimal](18, 2) NULL,
	[Reason] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[StandardQuantity] [decimal](18, 4) NOT NULL,
	[ActualQuantity] [decimal](18, 4) NOT NULL,
	[VarianceQuantity] [decimal](18, 4) NOT NULL,
	[VarianceCost] [decimal](18, 4) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MaterialConsumptionVariances] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MaterialIssue]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MaterialIssue](
	[Id] [uniqueidentifier] NOT NULL,
	[IssueNo] [nvarchar](255) NULL,
	[ProductionOrderId] [uniqueidentifier] NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[IssueDate] [datetime] NOT NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[IssueNumber] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MaterialIssues] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MaterialIssueLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MaterialIssueLine](
	[Id] [uniqueidentifier] NOT NULL,
	[MaterialIssueId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[LotId] [uniqueidentifier] NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UomId] [uniqueidentifier] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MaterialLotId] [uniqueidentifier] NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MaterialIssueLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MaterialLot]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MaterialLot](
	[Id] [uniqueidentifier] NOT NULL,
	[LotNo] [nvarchar](255) NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[SupplierId] [uniqueidentifier] NULL,
	[ManufactureDate] [datetime] NULL,
	[ExpiryDate] [datetime] NULL,
	[ReceivedDate] [datetime] NULL,
	[UnitCost] [decimal](18, 2) NULL,
	[QcStatus] [nvarchar](50) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[GoodsReceiptLineId] [uniqueidentifier] NULL,
	[LotNumber] [nvarchar](max) NULL,
	[QualityStatus] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MaterialLots] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MaterialRequirement]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MaterialRequirement](
	[Id] [uniqueidentifier] NOT NULL,
	[RequirementNo] [nvarchar](255) NULL,
	[ProductionPlanId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[RequiredQty] [decimal](18, 6) NOT NULL,
	[AvailableQty] [decimal](18, 6) NULL,
	[ExpectedIncomingQty] [decimal](18, 6) NULL,
	[PurchaseQty] [decimal](18, 6) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[RequiredQuantity] [decimal](18, 4) NOT NULL,
	[AvailableQuantity] [decimal](18, 4) NOT NULL,
	[ExpectedIncomingQuantity] [decimal](18, 4) NOT NULL,
	[PurchaseQuantity] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MaterialRequirements] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MaterialReservation]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MaterialReservation](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionOrderId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[RequiredQty] [decimal](18, 6) NOT NULL,
	[ReservedQty] [decimal](18, 6) NOT NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ReservedQuantity] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MaterialReservations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MealCost]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MealCost](
	[Id] [uniqueidentifier] NOT NULL,
	[OrderId] [uniqueidentifier] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[MealQty] [decimal](18, 6) NULL,
	[TotalCost] [decimal](18, 2) NULL,
	[CostPerMeal] [decimal](18, 2) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[OrderLineId] [uniqueidentifier] NULL,
	[AcceptedMealQuantity] [decimal](18, 4) NOT NULL,
	[TotalActualCost] [decimal](18, 4) NOT NULL,
	[Revenue] [decimal](18, 4) NULL,
	[GrossMargin] [decimal](18, 4) NULL,
	[CalculatedAt] [datetime2](7) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MealCosts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MealWasteRecord]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MealWasteRecord](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionBatchId] [uniqueidentifier] NULL,
	[ProductionOrderId] [uniqueidentifier] NULL,
	[DishId] [uniqueidentifier] NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[WasteType] [nvarchar](50) NOT NULL,
	[QuantityMeals] [decimal](18, 3) NULL,
	[QuantityKg] [decimal](18, 3) NULL,
	[Reason] [nvarchar](1000) NULL,
	[Disposition] [nvarchar](500) NULL,
	[OccurredAt] [datetime2](0) NOT NULL,
	[RecordedBy] [uniqueidentifier] NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_MealWasteRecord] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MedicalExamination]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MedicalExamination](
	[Id] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](max) NULL,
	[Price] [decimal](18, 0) NOT NULL,
	[NormalIndex] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_MedicalExamination] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MedicineWarehouse]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MedicineWarehouse](
	[Id] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](max) NULL,
	[Quantity] [int] NOT NULL,
	[SupplierId] [uniqueidentifier] NULL,
	[UnitMeasureId] [uniqueidentifier] NULL,
	[PriceInput] [decimal](18, 0) NOT NULL,
	[DateInput] [datetime] NULL,
	[Price] [decimal](18, 0) NOT NULL,
	[Expiry] [datetime] NULL,
	[PriceTotal] [decimal](18, 0) NOT NULL,
	[Note] [nvarchar](max) NULL,
	[IsMain] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_MedicineWarehouse] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Menu]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Menu](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[VersionNo] [int] NOT NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[MenuType] [nvarchar](max) NULL,
	[VersionCode] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Menus] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MenuAssignment]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MenuAssignment](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[DeliveryLocationId] [uniqueidentifier] NULL,
	[MenuId] [uniqueidentifier] NOT NULL,
	[MealType] [nvarchar](100) NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ContractId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MenuAssignments] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MenuChangeRequest]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MenuChangeRequest](
	[Id] [uniqueidentifier] NOT NULL,
	[RequestNo] [nvarchar](50) NOT NULL,
	[OrderId] [uniqueidentifier] NULL,
	[MenuId] [uniqueidentifier] NULL,
	[OriginalDishId] [uniqueidentifier] NULL,
	[RequestedDishId] [uniqueidentifier] NULL,
	[MealDate] [date] NOT NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[Quantity] [decimal](18, 3) NULL,
	[Reason] [nvarchar](1000) NOT NULL,
	[RequestedBy] [uniqueidentifier] NULL,
	[RequestStatus] [nvarchar](30) NOT NULL,
	[ApprovedBy] [uniqueidentifier] NULL,
	[ApprovedAt] [datetime2](0) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_MenuChangeRequest] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_MenuChangeRequest_RequestNo] UNIQUE NONCLUSTERED 
(
	[RequestNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MenuLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MenuLine](
	[Id] [uniqueidentifier] NOT NULL,
	[MenuId] [uniqueidentifier] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[MealType] [nvarchar](100) NULL,
	[SequenceNo] [int] NULL,
	[PortionQty] [decimal](18, 6) NULL,
	[IsRequired] [bit] NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DayOfWeek] [int] NULL,
	[SortOrder] [int] NOT NULL,
	[StandardQuantity] [decimal](18, 4) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_MenuLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ObjectMenu]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ObjectMenu](
	[Id] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](max) NULL,
	[ShortName] [nvarchar](max) NULL,
	[ParentId] [uniqueidentifier] NULL,
	[MetaLink] [nvarchar](max) NULL,
	[IconName] [nvarchar](max) NULL,
	[LevelShow] [int] NULL,
	[OrderShow] [int] NULL,
	[DataTarget] [nvarchar](max) NULL,
	[AriaControls] [nvarchar](max) NULL,
	[IsActive] [bit] NOT NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ObjectMenu] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Order]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Order](
	[Id] [uniqueidentifier] NOT NULL,
	[OrderNo] [nvarchar](255) NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[ContractId] [uniqueidentifier] NULL,
	[DeliveryLocationId] [uniqueidentifier] NULL,
	[OrderDate] [datetime] NOT NULL,
	[MealDate] [datetime] NOT NULL,
	[Status] [nvarchar](50) NULL,
	[TotalQuantity] [decimal](18, 6) NULL,
	[TotalAmount] [decimal](18, 2) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[OrderNumber] [nvarchar](max) NULL,
	[DeliveryDate] [datetime2](7) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrderLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrderLine](
	[Id] [uniqueidentifier] NOT NULL,
	[OrderId] [uniqueidentifier] NOT NULL,
	[MenuId] [uniqueidentifier] NULL,
	[MealType] [nvarchar](100) NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UnitPrice] [decimal](18, 2) NULL,
	[Amount] [decimal](18, 2) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ContractMealId] [uniqueidentifier] NULL,
	[LineAmount] [decimal](18, 4) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_OrderLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrderReconciliation]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrderReconciliation](
	[Id] [uniqueidentifier] NOT NULL,
	[OrderId] [uniqueidentifier] NOT NULL,
	[OrderedQty] [decimal](18, 6) NULL,
	[ProducedQty] [decimal](18, 6) NULL,
	[PackedQty] [decimal](18, 6) NULL,
	[DeliveredQty] [decimal](18, 6) NULL,
	[AcceptedQty] [decimal](18, 6) NULL,
	[VarianceQty] [decimal](18, 6) NULL,
	[VariancePercent] [decimal](18, 6) NULL,
	[Status] [nvarchar](50) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ProofOfDeliveryId] [uniqueidentifier] NULL,
	[ReconciliationDate] [datetime2](7) NOT NULL,
	[OrderedQuantity] [decimal](18, 4) NOT NULL,
	[DeliveredQuantity] [decimal](18, 4) NOT NULL,
	[AcceptedQuantity] [decimal](18, 4) NOT NULL,
	[FinalAmount] [decimal](18, 4) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_OrderReconciliations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OrganizationUnit]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OrganizationUnit](
	[Id] [uniqueidentifier] NOT NULL,
	[CompanyId] [uniqueidentifier] NOT NULL,
	[ParentId] [uniqueidentifier] NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[UnitType] [nvarchar](100) NULL,
	[ManagerId] [uniqueidentifier] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[Address] [nvarchar](max) NULL,
	[Phone] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_OrganizationUnits] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[OutboundMessage]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[OutboundMessage](
	[Id] [uniqueidentifier] NOT NULL,
	[IntegrationChannelId] [uniqueidentifier] NOT NULL,
	[ExternalConversationId] [nvarchar](255) NULL,
	[RecipientExternalId] [nvarchar](255) NULL,
	[MessageType] [nvarchar](50) NULL,
	[PayloadJson] [nvarchar](max) NOT NULL,
	[QueuedAt] [datetime2](0) NOT NULL,
	[SentAt] [datetime2](0) NULL,
	[MessageStatus] [nvarchar](30) NOT NULL,
	[RetryCount] [int] NOT NULL,
	[IdempotencyKey] [nvarchar](200) NULL,
	[CorrelationId] [nvarchar](100) NULL,
	[ProviderMessageId] [nvarchar](255) NULL,
	[LastError] [nvarchar](2000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_OutboundMessage] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PackagingLedger]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PackagingLedger](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NULL,
	[DeliveryLocationId] [uniqueidentifier] NULL,
	[OrderId] [uniqueidentifier] NULL,
	[DeliveryRouteId] [uniqueidentifier] NULL,
	[TransactionType] [nvarchar](30) NOT NULL,
	[PackagingType] [nvarchar](100) NOT NULL,
	[Quantity] [decimal](18, 3) NOT NULL,
	[TransactionAt] [datetime2](0) NOT NULL,
	[ReferenceNo] [nvarchar](100) NULL,
	[ConditionCode] [nvarchar](30) NULL,
	[RecordedBy] [uniqueidentifier] NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_PackagingLedger] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PackingOrder]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PackingOrder](
	[Id] [uniqueidentifier] NOT NULL,
	[PackingNo] [nvarchar](255) NULL,
	[ProductionBatchId] [uniqueidentifier] NOT NULL,
	[OrderId] [uniqueidentifier] NULL,
	[PlannedQty] [decimal](18, 6) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[PackingOrderNumber] [nvarchar](max) NULL,
	[PackingDate] [datetime2](7) NOT NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PackingOrders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PackingResult]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PackingResult](
	[Id] [uniqueidentifier] NOT NULL,
	[PackingOrderId] [uniqueidentifier] NOT NULL,
	[PlannedQty] [decimal](18, 6) NULL,
	[PackedQty] [decimal](18, 6) NULL,
	[DamagedQty] [decimal](18, 6) NULL,
	[RejectedQty] [decimal](18, 6) NULL,
	[AcceptedQty] [decimal](18, 6) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ProductionOutputId] [uniqueidentifier] NULL,
	[PackedQuantity] [decimal](18, 4) NOT NULL,
	[RejectedQuantity] [decimal](18, 4) NOT NULL,
	[PackagingType] [nvarchar](max) NULL,
	[QualityStatus] [nvarchar](max) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PackingResults] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Patient]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Patient](
	[Id] [uniqueidentifier] NOT NULL,
	[CouponCode] [bigint] NOT NULL,
	[FullName] [nvarchar](max) NULL,
	[DateOfBirth] [datetime] NULL,
	[Sex] [nvarchar](max) NULL,
	[PhoneNumber] [nvarchar](max) NULL,
	[AdministrativeProvinceId] [uniqueidentifier] NULL,
	[AdministrativeDistrictId] [uniqueidentifier] NULL,
	[AdministrativeWardId] [uniqueidentifier] NULL,
	[Address] [nvarchar](max) NULL,
	[Diagnosis] [nvarchar](max) NULL,
	[Result] [nvarchar](max) NULL,
	[Visit] [int] NOT NULL,
	[TotalPrice] [decimal](18, 0) NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Patient] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PatientDetail]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PatientDetail](
	[Id] [uniqueidentifier] NOT NULL,
	[PatientId] [uniqueidentifier] NULL,
	[MedicalExaminationId] [uniqueidentifier] NULL,
	[PersonId] [uniqueidentifier] NULL,
	[Price] [decimal](18, 0) NOT NULL,
	[Quantity] [int] NOT NULL,
	[TotalPrice] [decimal](18, 0) NOT NULL,
	[Result] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_PatientDetail] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Person]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Person](
	[Id] [uniqueidentifier] NOT NULL,
	[FullName] [nvarchar](max) NULL,
	[Avatar] [nvarchar](max) NULL,
	[PhoneNumber] [nvarchar](max) NULL,
	[DateOfBirth] [datetime] NULL,
	[Sex] [nvarchar](max) NULL,
	[Address] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Person] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Position]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Position](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[Description] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Positions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PricePolicie]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PricePolicie](
	[Id] [uniqueidentifier] NOT NULL,
	[CustomerId] [uniqueidentifier] NOT NULL,
	[ContractId] [uniqueidentifier] NULL,
	[MealType] [nvarchar](100) NULL,
	[UnitPrice] [decimal](18, 2) NOT NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[Code] [nvarchar](max) NULL,
	[Name] [nvarchar](max) NULL,
	[CurrencyCode] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PricePolicies] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductionBatche]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductionBatche](
	[Id] [uniqueidentifier] NOT NULL,
	[BatchNo] [nvarchar](255) NULL,
	[ProductionOrderId] [uniqueidentifier] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[RecipeId] [uniqueidentifier] NOT NULL,
	[StartTime] [datetime] NULL,
	[EndTime] [datetime] NULL,
	[PlannedQty] [decimal](18, 6) NULL,
	[ActualQty] [decimal](18, 6) NULL,
	[AcceptedQty] [decimal](18, 6) NULL,
	[RejectedQty] [decimal](18, 6) NULL,
	[WasteQty] [decimal](18, 6) NULL,
	[QcStatus] [nvarchar](50) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[BatchNumber] [nvarchar](max) NULL,
	[StartDate] [datetime2](7) NOT NULL,
	[EndDate] [datetime2](7) NULL,
	[PlannedQuantity] [decimal](18, 4) NULL,
	[ActualQuantity] [decimal](18, 4) NULL,
	[QualityStatus] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[KitchenWorkstationId] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ProductionBatches] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductionLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductionLine](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[CapacityMealPerBatch] [decimal](18, 2) NULL,
	[MinutesPerBatch] [decimal](10, 2) NULL,
	[Status] [nvarchar](30) NOT NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[OrganizationUnitId] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ProductionLine] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ProductionLine_Code] UNIQUE NONCLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductionOrder]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductionOrder](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionOrderNo] [nvarchar](255) NULL,
	[ProductionPlanId] [uniqueidentifier] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[RecipeId] [uniqueidentifier] NOT NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[PlannedQty] [decimal](18, 6) NOT NULL,
	[ActualQty] [decimal](18, 6) NULL,
	[Status] [nvarchar](50) NULL,
	[StartTime] [datetime] NULL,
	[EndTime] [datetime] NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ProductionOrderNumber] [nvarchar](max) NULL,
	[PlannedStartDate] [datetime2](7) NOT NULL,
	[ActualStartDate] [datetime2](7) NULL,
	[ActualEndDate] [datetime2](7) NULL,
	[ResponsibleEmployeeId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[ProductionLineId] [uniqueidentifier] NULL,
	[RecipeSnapshotJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_ProductionOrders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductionOutput]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductionOutput](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionBatchId] [uniqueidentifier] NOT NULL,
	[OutputQty] [decimal](18, 6) NULL,
	[AcceptedQty] [decimal](18, 6) NULL,
	[RejectedQty] [decimal](18, 6) NULL,
	[WasteQty] [decimal](18, 6) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[ProducedQuantity] [decimal](18, 4) NOT NULL,
	[AcceptedQuantity] [decimal](18, 4) NOT NULL,
	[RejectedQuantity] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NULL,
	[QualityStatus] [nvarchar](max) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ProductionOutputs] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductionPlan]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductionPlan](
	[Id] [uniqueidentifier] NOT NULL,
	[PlanNo] [nvarchar](255) NULL,
	[ProductionDate] [datetime] NOT NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[PlanNumber] [nvarchar](max) NULL,
	[PlanDate] [datetime2](7) NOT NULL,
	[OrganizationUnitId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ProductionPlans] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProductionPlanLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProductionPlanLine](
	[Id] [uniqueidentifier] NOT NULL,
	[ProductionPlanId] [uniqueidentifier] NOT NULL,
	[OrderLineId] [uniqueidentifier] NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[PlannedQty] [decimal](18, 6) NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[PlannedQuantity] [decimal](18, 4) NOT NULL,
	[ActualQuantity] [decimal](18, 4) NULL,
	[ShiftId] [uniqueidentifier] NULL,
	[Status] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ProductionPlanLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProofOfDeliverie]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProofOfDeliverie](
	[Id] [uniqueidentifier] NOT NULL,
	[DeliveryOrderId] [uniqueidentifier] NOT NULL,
	[DeliveredQty] [decimal](18, 6) NULL,
	[AcceptedQty] [decimal](18, 6) NULL,
	[RejectedQty] [decimal](18, 6) NULL,
	[ReceiverName] [nvarchar](500) NULL,
	[ReceiverPhone] [nvarchar](100) NULL,
	[ReceivedAt] [datetime] NULL,
	[SignatureUrl] [nvarchar](max) NULL,
	[PhotoUrl] [nvarchar](max) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[ConfirmedAt] [datetime2](7) NOT NULL,
	[DeliveredQuantity] [decimal](18, 4) NULL,
	[AcceptedQuantity] [decimal](18, 4) NULL,
	[RejectedQuantity] [decimal](18, 4) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ProofOfDeliveries] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PurchaseOrder]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PurchaseOrder](
	[Id] [uniqueidentifier] NOT NULL,
	[PONo] [nvarchar](255) NULL,
	[SupplierId] [uniqueidentifier] NOT NULL,
	[PurchaseRequestId] [uniqueidentifier] NULL,
	[OrderDate] [datetime] NOT NULL,
	[ExpectedDate] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[TotalAmount] [decimal](18, 2) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[PurchaseOrderNumber] [nvarchar](max) NULL,
	[ExpectedDeliveryDate] [datetime2](7) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PurchaseOrders] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PurchaseOrderLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PurchaseOrderLine](
	[Id] [uniqueidentifier] NOT NULL,
	[PurchaseOrderId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UomId] [uniqueidentifier] NOT NULL,
	[UnitPrice] [decimal](18, 2) NOT NULL,
	[Amount] [decimal](18, 2) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[LineAmount] [decimal](18, 4) NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PurchaseOrderLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PurchaseRequest]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PurchaseRequest](
	[Id] [uniqueidentifier] NOT NULL,
	[RequestNo] [nvarchar](255) NULL,
	[RequestDate] [datetime] NOT NULL,
	[RequesterId] [uniqueidentifier] NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[RequestNumber] [nvarchar](max) NULL,
	[RequestedByEmployeeId] [uniqueidentifier] NULL,
	[RequiredDate] [datetime2](7) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PurchaseRequests] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PurchaseRequestLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PurchaseRequestLine](
	[Id] [uniqueidentifier] NOT NULL,
	[PurchaseRequestId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UomId] [uniqueidentifier] NOT NULL,
	[RequiredDate] [datetime] NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_PurchaseRequestLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QCInspection]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QCInspection](
	[Id] [uniqueidentifier] NOT NULL,
	[GoodsReceiptId] [uniqueidentifier] NOT NULL,
	[InspectionDate] [datetime] NOT NULL,
	[InspectorId] [uniqueidentifier] NULL,
	[Result] [nvarchar](50) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[InspectionNumber] [nvarchar](max) NULL,
	[InspectorEmployeeId] [uniqueidentifier] NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_QCInspections] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[QCInspectionLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[QCInspectionLine](
	[Id] [uniqueidentifier] NOT NULL,
	[QCInspectionId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[Temperature] [decimal](10, 2) NULL,
	[Weight] [decimal](18, 6) NULL,
	[Appearance] [nvarchar](max) NULL,
	[Packaging] [nvarchar](max) NULL,
	[ExpiryDate] [datetime] NULL,
	[Result] [nvarchar](50) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[GoodsReceiptLineId] [uniqueidentifier] NULL,
	[Criterion] [nvarchar](max) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_QCInspectionLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Recipe]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Recipe](
	[Id] [uniqueidentifier] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[VersionNo] [int] NOT NULL,
	[YieldPercent] [decimal](18, 6) NULL,
	[StandardOutputQty] [decimal](18, 6) NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[ApprovedBy] [uniqueidentifier] NULL,
	[ApprovedAt] [datetime] NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[VersionCode] [nvarchar](max) NULL,
	[YieldQuantity] [decimal](18, 4) NOT NULL,
	[YieldUnitId] [uniqueidentifier] NULL,
	[IsActive] [bit] NOT NULL,
	[Instructions] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[SnapshotJson] [nvarchar](max) NULL,
	[LockedAt] [datetime2](0) NULL,
	[LockedBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Recipes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RecipeLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RecipeLine](
	[Id] [uniqueidentifier] NOT NULL,
	[RecipeId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[Quantity] [decimal](18, 6) NOT NULL,
	[UomId] [uniqueidentifier] NOT NULL,
	[WastePercent] [decimal](18, 6) NULL,
	[YieldPercent] [decimal](18, 6) NULL,
	[SequenceNo] [int] NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[ExpectedWasteRate] [decimal](18, 4) NULL,
	[PreparationNote] [nvarchar](max) NULL,
	[SortOrder] [int] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_RecipeLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ReturnInspection]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ReturnInspection](
	[Id] [uniqueidentifier] NOT NULL,
	[ContainerReturnId] [uniqueidentifier] NOT NULL,
	[InspectionResult] [nvarchar](50) NULL,
	[DamagedQty] [decimal](18, 6) NULL,
	[MissingQty] [decimal](18, 6) NULL,
	[Remark] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[InspectionDate] [datetime2](7) NOT NULL,
	[InspectorEmployeeId] [uniqueidentifier] NULL,
	[Result] [nvarchar](max) NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_ReturnInspections] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Shift]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Shift](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](255) NULL,
	[StartTime] [time](7) NULL,
	[EndTime] [time](7) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Shifts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ShiftAssignment]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ShiftAssignment](
	[Id] [uniqueidentifier] NOT NULL,
	[EmployeeId] [uniqueidentifier] NOT NULL,
	[ShiftId] [uniqueidentifier] NOT NULL,
	[WorkDate] [date] NOT NULL,
	[PlannedStart] [datetime2](0) NULL,
	[PlannedEnd] [datetime2](0) NULL,
	[AssignmentStatus] [nvarchar](30) NOT NULL,
	[AssignedBy] [uniqueidentifier] NULL,
	[Note] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ShiftAssignment] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ShiftAssignment_Employee_Shift_Date] UNIQUE NONCLUSTERED 
(
	[EmployeeId] ASC,
	[ShiftId] ASC,
	[WorkDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ShiftSwapRequest]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ShiftSwapRequest](
	[Id] [uniqueidentifier] NOT NULL,
	[RequestNo] [nvarchar](50) NOT NULL,
	[RequesterEmployeeId] [uniqueidentifier] NOT NULL,
	[TargetEmployeeId] [uniqueidentifier] NOT NULL,
	[CurrentShiftAssignmentId] [uniqueidentifier] NOT NULL,
	[ProposedShiftAssignmentId] [uniqueidentifier] NULL,
	[Reason] [nvarchar](1000) NULL,
	[RequestStatus] [nvarchar](30) NOT NULL,
	[ApprovalRequestId] [uniqueidentifier] NULL,
	[SubmittedAt] [datetime2](0) NOT NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_ShiftSwapRequest] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_ShiftSwapRequest_RequestNo] UNIQUE NONCLUSTERED 
(
	[RequestNo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StandardCost]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StandardCost](
	[Id] [uniqueidentifier] NOT NULL,
	[DishId] [uniqueidentifier] NOT NULL,
	[RecipeId] [uniqueidentifier] NOT NULL,
	[MaterialCost] [decimal](18, 2) NULL,
	[LaborCost] [decimal](18, 2) NULL,
	[OverheadCost] [decimal](18, 2) NULL,
	[TotalCost] [decimal](18, 2) NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CostPerServing] [decimal](18, 4) NOT NULL,
	[CurrencyCode] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_StandardCosts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StockCount]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StockCount](
	[Id] [uniqueidentifier] NOT NULL,
	[CountNo] [nvarchar](255) NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[CountType] [nvarchar](50) NULL,
	[CountDate] [datetime] NOT NULL,
	[Status] [nvarchar](50) NULL,
	[ApprovedBy] [uniqueidentifier] NULL,
	[ApprovedAt] [datetime] NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CountNumber] [nvarchar](max) NULL,
	[ResponsibleEmployeeId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_StockCounts] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StockCountLine]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StockCountLine](
	[Id] [uniqueidentifier] NOT NULL,
	[StockCountId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[LotId] [uniqueidentifier] NULL,
	[SystemQty] [decimal](18, 6) NULL,
	[PhysicalQty] [decimal](18, 6) NULL,
	[VarianceQty] [decimal](18, 6) NULL,
	[VarianceValue] [decimal](18, 2) NULL,
	[Reason] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MaterialLotId] [uniqueidentifier] NULL,
	[SystemQuantity] [decimal](18, 4) NOT NULL,
	[PhysicalQuantity] [decimal](18, 4) NOT NULL,
	[VarianceQuantity] [decimal](18, 4) NOT NULL,
	[UnitId] [uniqueidentifier] NOT NULL,
	[VarianceReason] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_StockCountLines] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StorageLocation]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StorageLocation](
	[Id] [uniqueidentifier] NOT NULL,
	[ZoneId] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](255) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[StorageZoneId] [uniqueidentifier] NULL,
	[LocationType] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_StorageLocations] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StorageZone]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StorageZone](
	[Id] [uniqueidentifier] NOT NULL,
	[WarehouseId] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](255) NULL,
	[TemperatureMin] [decimal](10, 2) NULL,
	[TemperatureMax] [decimal](10, 2) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[TemperatureRange] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_StorageZones] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Supplier]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Supplier](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[SupplierType] [nvarchar](100) NULL,
	[TaxCode] [nvarchar](100) NULL,
	[Address] [nvarchar](max) NULL,
	[Phone] [nvarchar](100) NULL,
	[Email] [nvarchar](255) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[ContactName] [nvarchar](max) NULL,
	[ContactPhone] [nvarchar](100) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Supplier] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SupplierIssue]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SupplierIssue](
	[Id] [uniqueidentifier] NOT NULL,
	[SupplierId] [uniqueidentifier] NOT NULL,
	[PurchaseOrderId] [uniqueidentifier] NULL,
	[GoodsReceiptId] [uniqueidentifier] NULL,
	[IssueType] [nvarchar](50) NOT NULL,
	[Severity] [nvarchar](30) NOT NULL,
	[Description] [nvarchar](2000) NOT NULL,
	[ReportedAt] [datetime2](0) NOT NULL,
	[DueAt] [datetime2](0) NULL,
	[IssueStatus] [nvarchar](30) NOT NULL,
	[Resolution] [nvarchar](2000) NULL,
	[ClosedAt] [datetime2](0) NULL,
	[OwnerId] [uniqueidentifier] NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_SupplierIssue] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SupplierMaterial]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SupplierMaterial](
	[Id] [uniqueidentifier] NOT NULL,
	[SupplierId] [uniqueidentifier] NOT NULL,
	[IngredientId] [uniqueidentifier] NOT NULL,
	[SupplierMaterialCode] [nvarchar](255) NULL,
	[PurchaseUomId] [uniqueidentifier] NULL,
	[UnitPrice] [decimal](18, 2) NULL,
	[MinimumOrderQty] [decimal](18, 6) NULL,
	[LeadTimeDays] [int] NULL,
	[DeliveryFrequency] [nvarchar](100) NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[QualityRequirement] [nvarchar](max) NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[MinimumOrderQuantity] [decimal](18, 4) NULL,
	[IsPreferred] [bit] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_SupplierMaterials] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemperatureLog]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemperatureLog](
	[Id] [uniqueidentifier] NOT NULL,
	[StorageZoneId] [uniqueidentifier] NULL,
	[StorageLocationId] [uniqueidentifier] NULL,
	[MaterialLotId] [uniqueidentifier] NULL,
	[ProductionBatchId] [uniqueidentifier] NULL,
	[MeasurementType] [nvarchar](40) NOT NULL,
	[TemperatureC] [decimal](6, 2) NOT NULL,
	[MinAllowedC] [decimal](6, 2) NULL,
	[MaxAllowedC] [decimal](6, 2) NULL,
	[IsWithinRange] [bit] NULL,
	[MeasuredAt] [datetime2](0) NOT NULL,
	[DeviceCode] [nvarchar](100) NULL,
	[SourceChannel] [nvarchar](50) NULL,
	[RecordedBy] [uniqueidentifier] NULL,
	[CorrectiveAction] [nvarchar](2000) NULL,
	[EvidenceUrl] [nvarchar](1000) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_TemperatureLog] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TestDemo]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TestDemo](
	[Id] [uniqueidentifier] NOT NULL,
	[FieldString] [nvarchar](max) NULL,
	[FieldDateTime] [datetime] NULL,
	[FieldDropdownId] [uniqueidentifier] NULL,
	[FieldNumber] [int] NULL,
	[FieldTextArea] [nvarchar](max) NULL,
	[FieldImageSingle] [nvarchar](max) NULL,
	[FieldCheckBox] [bit] NULL,
	[FieldSwitch] [bit] NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_TestDemo] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UnitMeasure]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UnitMeasure](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](50) NULL,
	[Name] [nvarchar](255) NULL,
	[UomType] [nvarchar](100) NULL,
	[DecimalPrecision] [int] NULL,
	[Status] [nvarchar](50) NULL,
	[Symbol] [nvarchar](max) NULL,
	[UnitType] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_UnitMeasure] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UnitsOfMeasure_Legacy_20261113]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UnitsOfMeasure_Legacy_20261113](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](50) NULL,
	[Name] [nvarchar](255) NULL,
	[UomType] [nvarchar](100) NULL,
	[DecimalPrecision] [int] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[Symbol] [nvarchar](max) NULL,
	[UnitType] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_UnitsOfMeasure] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UomConversion]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UomConversion](
	[Id] [uniqueidentifier] NOT NULL,
	[FromUomId] [uniqueidentifier] NOT NULL,
	[ToUomId] [uniqueidentifier] NOT NULL,
	[ConversionFactor] [decimal](28, 10) NOT NULL,
	[EffectiveFrom] [datetime] NULL,
	[EffectiveTo] [datetime] NULL,
	[Status] [nvarchar](50) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[FromUnitId] [uniqueidentifier] NOT NULL,
	[ToUnitId] [uniqueidentifier] NOT NULL,
	[IsActive] [bit] NOT NULL,
	[Note] [nvarchar](max) NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_UomConversions] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[User]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[User](
	[Id] [uniqueidentifier] NOT NULL,
	[UserName] [nvarchar](max) NULL,
	[PassWord] [nvarchar](max) NULL,
	[PersonId] [uniqueidentifier] NULL,
	[IsActive] [bit] NOT NULL,
	[IsAdmin] [bit] NOT NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserClaim]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserClaim](
	[Id] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NULL,
	[UserGroupId] [uniqueidentifier] NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UserClaim] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserExternalIdentity]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserExternalIdentity](
	[Id] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NULL,
	[EmployeeId] [uniqueidentifier] NULL,
	[IntegrationChannelId] [uniqueidentifier] NOT NULL,
	[ExternalUserId] [nvarchar](255) NOT NULL,
	[ExternalUsername] [nvarchar](255) NULL,
	[DisplayName] [nvarchar](255) NULL,
	[LinkedAt] [datetime2](0) NOT NULL,
	[IdentityStatus] [nvarchar](30) NOT NULL,
	[LastSeenAt] [datetime2](0) NULL,
	[CreatedDate] [datetime2](0) NOT NULL,
	[CreatedBy] [uniqueidentifier] NULL,
	[ModifiedDate] [datetime2](0) NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UserExternalIdentity] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_UserExternalIdentity_Channel_User] UNIQUE NONCLUSTERED 
(
	[IntegrationChannelId] ASC,
	[ExternalUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserGroup]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserGroup](
	[Id] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](max) NULL,
	[IsDefault] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UserGroup] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserRole]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserRole](
	[Id] [uniqueidentifier] NOT NULL,
	[UserGroupId] [uniqueidentifier] NULL,
	[ObjectMenuId] [uniqueidentifier] NULL,
	[AllowAccess] [bit] NOT NULL,
	[AllowAdd] [bit] NOT NULL,
	[AllowEdit] [bit] NOT NULL,
	[AllowDelete] [bit] NOT NULL,
	[AllowPrint] [bit] NOT NULL,
	[AllowExport] [bit] NOT NULL,
	[AllowImport] [bit] NOT NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_UserRole] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Vehicle]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Vehicle](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[LicensePlate] [nvarchar](100) NULL,
	[VehicleType] [nvarchar](100) NULL,
	[Capacity] [decimal](18, 2) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
 CONSTRAINT [PK_Vehicles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Warehouse]    Script Date: 11/14/2026 12:00:44 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Warehouse](
	[Id] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](255) NULL,
	[Name] [nvarchar](max) NULL,
	[WarehouseType] [nvarchar](100) NULL,
	[Address] [nvarchar](max) NULL,
	[Status] [nvarchar](50) NULL,
	[Note] [nvarchar](max) NULL,
	[CreatedDate] [datetime] NOT NULL,
	[CreatedBy] [uniqueidentifier] NOT NULL,
	[ModifiedDate] [datetime] NULL,
	[ModifiedBy] [uniqueidentifier] NULL,
	[DeletedDate] [datetime] NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[IsDeleted] [bit] NOT NULL,
	[CompanyId] [uniqueidentifier] NULL,
	[OrganizationUnitId] [uniqueidentifier] NULL,
	[DeleteddBy] [uniqueidentifier] NULL,
 CONSTRAINT [PK_Warehouses] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
ALTER TABLE [dbo].[ActualCost] ADD  CONSTRAINT [DF_ActualCost_OtherCost]  DEFAULT ((0)) FOR [OtherCost]
GO
ALTER TABLE [dbo].[ActualCost] ADD  CONSTRAINT [DF_ActualCost_CalculatedAt]  DEFAULT (sysutcdatetime()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[ActualCosts_Legacy_20261113] ADD  CONSTRAINT [DF_ActualCosts_OtherCost]  DEFAULT ((0)) FOR [OtherCost]
GO
ALTER TABLE [dbo].[ActualCosts_Legacy_20261113] ADD  CONSTRAINT [DF_ActualCosts_CalculatedAt]  DEFAULT (sysutcdatetime()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[AdministrativeDistrict] ADD  CONSTRAINT [DF_AdministrativeDistrict_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AdministrativeDistrict] ADD  CONSTRAINT [DF_AdministrativeDistrict_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[AdministrativeDistrict] ADD  CONSTRAINT [DF_AdministrativeDistrict_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AdministrativeDistrict] ADD  CONSTRAINT [DF_AdministrativeDistrict_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AdministrativeProvince] ADD  CONSTRAINT [DF_AdministrativeProvince_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AdministrativeProvince] ADD  CONSTRAINT [DF_AdministrativeProvince_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[AdministrativeProvince] ADD  CONSTRAINT [DF_AdministrativeProvince_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AdministrativeProvince] ADD  CONSTRAINT [DF_AdministrativeProvince_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AdministrativeRegion] ADD  CONSTRAINT [DF_AdministrativeRegion_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AdministrativeRegion] ADD  CONSTRAINT [DF_AdministrativeRegion_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[AdministrativeRegion] ADD  CONSTRAINT [DF_AdministrativeRegion_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AdministrativeRegion] ADD  CONSTRAINT [DF_AdministrativeRegion_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AdministrativeUnit] ADD  CONSTRAINT [DF_AdministrativeUnit_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AdministrativeUnit] ADD  CONSTRAINT [DF_AdministrativeUnit_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[AdministrativeUnit] ADD  CONSTRAINT [DF_AdministrativeUnit_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AdministrativeUnit] ADD  CONSTRAINT [DF_AdministrativeUnit_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AdministrativeWard] ADD  CONSTRAINT [DF_AdministrativeWard_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AdministrativeWard] ADD  CONSTRAINT [DF_AdministrativeWard_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[AdministrativeWard] ADD  CONSTRAINT [DF_AdministrativeWard_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AdministrativeWard] ADD  CONSTRAINT [DF_AdministrativeWard_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AnalyticsRefreshLog] ADD  CONSTRAINT [DF_AnalyticsRefreshLog_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[AnalyticsRefreshLog] ADD  CONSTRAINT [DF_AnalyticsRefreshLog_StartedAt]  DEFAULT (sysdatetime()) FOR [StartedAt]
GO
ALTER TABLE [dbo].[AnalyticsRefreshLog] ADD  CONSTRAINT [DF_AnalyticsRefreshLog_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AnalyticsRefreshLog] ADD  CONSTRAINT [DF_AnalyticsRefreshLog_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ApprovalDecision] ADD  CONSTRAINT [DF_ApprovalDecision_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ApprovalDecision] ADD  CONSTRAINT [DF_ApprovalDecision_DecidedAt]  DEFAULT (sysdatetime()) FOR [DecidedAt]
GO
ALTER TABLE [dbo].[ApprovalDecision] ADD  CONSTRAINT [DF_ApprovalDecision_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ApprovalDecision] ADD  CONSTRAINT [DF_ApprovalDecision_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ApprovalItem] ADD  CONSTRAINT [DF_ApprovalItem_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ApprovalItem] ADD  CONSTRAINT [DF_ApprovalItem_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ApprovalItem] ADD  CONSTRAINT [DF_ApprovalItem_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ApprovalRequest] ADD  CONSTRAINT [DF_ApprovalRequest_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ApprovalRequest] ADD  CONSTRAINT [DF_ApprovalRequest_RequestedAt]  DEFAULT (sysdatetime()) FOR [RequestedAt]
GO
ALTER TABLE [dbo].[ApprovalRequest] ADD  CONSTRAINT [DF_ApprovalRequest_Status]  DEFAULT (N'Pending') FOR [RequestStatus]
GO
ALTER TABLE [dbo].[ApprovalRequest] ADD  CONSTRAINT [DF_ApprovalRequest_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ApprovalRequest] ADD  CONSTRAINT [DF_ApprovalRequest_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ApprovalStep] ADD  CONSTRAINT [DF_ApprovalStep_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ApprovalStep] ADD  CONSTRAINT [DF_ApprovalStep_Status]  DEFAULT (N'Waiting') FOR [DecisionStatus]
GO
ALTER TABLE [dbo].[ApprovalStep] ADD  CONSTRAINT [DF_ApprovalStep_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ApprovalStep] ADD  CONSTRAINT [DF_ApprovalStep_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AttendanceEvent] ADD  CONSTRAINT [DF_AttendanceEvent_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[AttendanceEvent] ADD  CONSTRAINT [DF_AttendanceEvent_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AttendanceEvent] ADD  CONSTRAINT [DF_AttendanceEvent_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[AuditChange] ADD  CONSTRAINT [DF_AuditChange_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AuditChange] ADD  CONSTRAINT [DF_AuditChange_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[AuditChange] ADD  CONSTRAINT [DF_AuditChange_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[BotConfiguration] ADD  CONSTRAINT [DF_BotConfiguration_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[BotConfiguration] ADD  CONSTRAINT [DF_BotConfiguration_Status]  DEFAULT (N'Inactive') FOR [BotStatus]
GO
ALTER TABLE [dbo].[BotConfiguration] ADD  CONSTRAINT [DF_BotConfiguration_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[BotConfiguration] ADD  CONSTRAINT [DF_BotConfiguration_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[CleaningBatche] ADD  CONSTRAINT [DF_CleaningBatches_StartDate]  DEFAULT (sysutcdatetime()) FOR [StartDate]
GO
ALTER TABLE [dbo].[CleaningBatche] ADD  CONSTRAINT [DF_CleaningBatches_ContainerQuantity]  DEFAULT ((0)) FOR [ContainerQuantity]
GO
ALTER TABLE [dbo].[CleaningChecklistResult] ADD  CONSTRAINT [DF_CleaningChecklistResult_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[CleaningChecklistResult] ADD  CONSTRAINT [DF_CleaningChecklistResult_CheckedAt]  DEFAULT (sysdatetime()) FOR [CheckedAt]
GO
ALTER TABLE [dbo].[CleaningChecklistResult] ADD  CONSTRAINT [DF_CleaningChecklistResult_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[CleaningChecklistResult] ADD  CONSTRAINT [DF_CleaningChecklistResult_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ComplaintHistory] ADD  CONSTRAINT [DF_ComplaintHistory_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ComplaintHistory] ADD  CONSTRAINT [DF_ComplaintHistory_EventAt]  DEFAULT (sysdatetime()) FOR [EventAt]
GO
ALTER TABLE [dbo].[ComplaintHistory] ADD  CONSTRAINT [DF_ComplaintHistory_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ComplaintHistory] ADD  CONSTRAINT [DF_ComplaintHistory_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ContainerReturn] ADD  CONSTRAINT [DF_ContainerReturns_ExpectedQuantity]  DEFAULT ((0)) FOR [ExpectedQuantity]
GO
ALTER TABLE [dbo].[ContainerReturn] ADD  CONSTRAINT [DF_ContainerReturns_ReturnedQuantity]  DEFAULT ((0)) FOR [ReturnedQuantity]
GO
ALTER TABLE [dbo].[ContainerReturn] ADD  CONSTRAINT [DF_ContainerReturns_DamagedQuantity]  DEFAULT ((0)) FOR [DamagedQuantity]
GO
ALTER TABLE [dbo].[Contract] ADD  CONSTRAINT [DF_Contracts_EffectiveFrom]  DEFAULT (sysutcdatetime()) FOR [EffectiveFrom]
GO
ALTER TABLE [dbo].[CoreDataMergeConflict] ADD  CONSTRAINT [DF_CoreDataMergeConflict_DetectedAt]  DEFAULT (sysutcdatetime()) FOR [DetectedAt]
GO
ALTER TABLE [dbo].[Customer] ADD  CONSTRAINT [DF_Customer_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[Customer] ADD  CONSTRAINT [DF_Customer_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Customer] ADD  CONSTRAINT [DF_Customer_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[CustomerComplaint] ADD  CONSTRAINT [DF_CustomerComplaint_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[CustomerComplaint] ADD  CONSTRAINT [DF_CustomerComplaint_Severity]  DEFAULT (N'Medium') FOR [Severity]
GO
ALTER TABLE [dbo].[CustomerComplaint] ADD  CONSTRAINT [DF_CustomerComplaint_ReceivedAt]  DEFAULT (sysdatetime()) FOR [ReceivedAt]
GO
ALTER TABLE [dbo].[CustomerComplaint] ADD  CONSTRAINT [DF_CustomerComplaint_Status]  DEFAULT (N'Open') FOR [ComplaintStatus]
GO
ALTER TABLE [dbo].[CustomerComplaint] ADD  CONSTRAINT [DF_CustomerComplaint_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[CustomerComplaint] ADD  CONSTRAINT [DF_CustomerComplaint_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[DeliveryEvent] ADD  CONSTRAINT [DF_DeliveryEvent_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[DeliveryEvent] ADD  CONSTRAINT [DF_DeliveryEvent_EventAt]  DEFAULT (sysdatetime()) FOR [EventAt]
GO
ALTER TABLE [dbo].[DeliveryEvent] ADD  CONSTRAINT [DF_DeliveryEvent_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[DeliveryEvent] ADD  CONSTRAINT [DF_DeliveryEvent_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[DeliveryLoading] ADD  CONSTRAINT [DF_DeliveryLoadings_LoadingDate]  DEFAULT (sysutcdatetime()) FOR [LoadingDate]
GO
ALTER TABLE [dbo].[DeliveryOrder] ADD  CONSTRAINT [DF_DeliveryOrders_PlannedDeliveryTime]  DEFAULT (sysutcdatetime()) FOR [PlannedDeliveryTime]
GO
ALTER TABLE [dbo].[DeliveryStop] ADD  CONSTRAINT [DF_DeliveryStop_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[DeliveryStop] ADD  CONSTRAINT [DF_DeliveryStop_Status]  DEFAULT (N'Planned') FOR [StopStatus]
GO
ALTER TABLE [dbo].[DeliveryStop] ADD  CONSTRAINT [DF_DeliveryStop_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[DeliveryStop] ADD  CONSTRAINT [DF_DeliveryStop_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[DocumentFile] ADD  CONSTRAINT [DF_DocumentFile_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[DocumentFile] ADD  CONSTRAINT [DF_DocumentFile_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[DocumentFile] ADD  CONSTRAINT [DF_DocumentFile_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[FoodSafetyChecklist] ADD  CONSTRAINT [DF_FoodSafetyChecklist_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[FoodSafetyChecklist] ADD  CONSTRAINT [DF_FoodSafetyChecklist_Version]  DEFAULT ((1)) FOR [VersionNo]
GO
ALTER TABLE [dbo].[FoodSafetyChecklist] ADD  CONSTRAINT [DF_FoodSafetyChecklist_Status]  DEFAULT (N'Active') FOR [Status]
GO
ALTER TABLE [dbo].[FoodSafetyChecklist] ADD  CONSTRAINT [DF_FoodSafetyChecklist_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[FoodSafetyChecklist] ADD  CONSTRAINT [DF_FoodSafetyChecklist_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistItem] ADD  CONSTRAINT [DF_FoodSafetyChecklistItem_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistItem] ADD  CONSTRAINT [DF_FoodSafetyChecklistItem_Sort]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistItem] ADD  CONSTRAINT [DF_FoodSafetyChecklistItem_Critical]  DEFAULT ((0)) FOR [IsCritical]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistItem] ADD  CONSTRAINT [DF_FoodSafetyChecklistItem_Required]  DEFAULT ((1)) FOR [IsRequired]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistItem] ADD  CONSTRAINT [DF_FoodSafetyChecklistItem_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistItem] ADD  CONSTRAINT [DF_FoodSafetyChecklistItem_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistResult] ADD  CONSTRAINT [DF_FoodSafetyChecklistResult_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistResult] ADD  CONSTRAINT [DF_FoodSafetyChecklistResult_CheckedAt]  DEFAULT (sysdatetime()) FOR [CheckedAt]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistResult] ADD  CONSTRAINT [DF_FoodSafetyChecklistResult_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[FoodSafetyChecklistResult] ADD  CONSTRAINT [DF_FoodSafetyChecklistResult_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Forecast] ADD  CONSTRAINT [DF_Forecasts_ForecastQuantity]  DEFAULT ((0)) FOR [ForecastQuantity]
GO
ALTER TABLE [dbo].[GoodsReceiptLine] ADD  CONSTRAINT [DF_GoodsReceiptLines_ReceivedQuantity]  DEFAULT ((0)) FOR [ReceivedQuantity]
GO
ALTER TABLE [dbo].[GoodsReceiptLine] ADD  CONSTRAINT [DF_GoodsReceiptLines_AcceptedQuantity]  DEFAULT ((0)) FOR [AcceptedQuantity]
GO
ALTER TABLE [dbo].[GoodsReceiptLine] ADD  CONSTRAINT [DF_GoodsReceiptLines_RejectedQuantity]  DEFAULT ((0)) FOR [RejectedQuantity]
GO
ALTER TABLE [dbo].[GoodsReceiptLine] ADD  CONSTRAINT [DF_GoodsReceiptLines_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[InboundMessage] ADD  CONSTRAINT [DF_InboundMessage_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[InboundMessage] ADD  CONSTRAINT [DF_InboundMessage_ReceivedAt]  DEFAULT (sysdatetime()) FOR [ReceivedAt]
GO
ALTER TABLE [dbo].[InboundMessage] ADD  CONSTRAINT [DF_InboundMessage_Status]  DEFAULT (N'Received') FOR [ProcessingStatus]
GO
ALTER TABLE [dbo].[InboundMessage] ADD  CONSTRAINT [DF_InboundMessage_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[InboundMessage] ADD  CONSTRAINT [DF_InboundMessage_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Ingredient] ADD  CONSTRAINT [DF_Ingredients_IsExpiryManaged]  DEFAULT ((0)) FOR [IsExpiryManaged]
GO
ALTER TABLE [dbo].[IntegrationChannel] ADD  CONSTRAINT [DF_IntegrationChannel_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[IntegrationChannel] ADD  CONSTRAINT [DF_IntegrationChannel_Environment]  DEFAULT (N'Production') FOR [EnvironmentCode]
GO
ALTER TABLE [dbo].[IntegrationChannel] ADD  CONSTRAINT [DF_IntegrationChannel_Status]  DEFAULT (N'Inactive') FOR [ChannelStatus]
GO
ALTER TABLE [dbo].[IntegrationChannel] ADD  CONSTRAINT [DF_IntegrationChannel_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[IntegrationChannel] ADD  CONSTRAINT [DF_IntegrationChannel_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[IntegrationLog] ADD  CONSTRAINT [DF_IntegrationLog_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[IntegrationLog] ADD  CONSTRAINT [DF_IntegrationLog_RequestAt]  DEFAULT (sysdatetime()) FOR [RequestAt]
GO
ALTER TABLE [dbo].[IntegrationLog] ADD  CONSTRAINT [DF_IntegrationLog_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[IntegrationLog] ADD  CONSTRAINT [DF_IntegrationLog_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[InventoryBalance] ADD  CONSTRAINT [DF_InventoryBalances_OnHandQuantity]  DEFAULT ((0)) FOR [OnHandQuantity]
GO
ALTER TABLE [dbo].[InventoryBalance] ADD  CONSTRAINT [DF_InventoryBalances_ReservedQuantity]  DEFAULT ((0)) FOR [ReservedQuantity]
GO
ALTER TABLE [dbo].[InventoryBalance] ADD  CONSTRAINT [DF_InventoryBalances_AvailableQuantity]  DEFAULT ((0)) FOR [AvailableQuantity]
GO
ALTER TABLE [dbo].[InventoryBalance] ADD  CONSTRAINT [DF_InventoryBalances_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[InventoryBalance] ADD  CONSTRAINT [DF_InventoryBalances_LastCalculatedDate]  DEFAULT (sysutcdatetime()) FOR [LastCalculatedDate]
GO
ALTER TABLE [dbo].[InventoryPolicie] ADD  CONSTRAINT [DF_InventoryPolicies_UseFefo]  DEFAULT ((0)) FOR [UseFefo]
GO
ALTER TABLE [dbo].[InventoryTransaction] ADD  CONSTRAINT [DF_InventoryTransactions_QuantityIn]  DEFAULT ((0)) FOR [QuantityIn]
GO
ALTER TABLE [dbo].[InventoryTransaction] ADD  CONSTRAINT [DF_InventoryTransactions_QuantityOut]  DEFAULT ((0)) FOR [QuantityOut]
GO
ALTER TABLE [dbo].[InventoryTransaction] ADD  CONSTRAINT [DF_InventoryTransactions_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[KitchenWorkstation] ADD  CONSTRAINT [DF_KitchenWorkstation_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[KitchenWorkstation] ADD  CONSTRAINT [DF_KitchenWorkstation_Status]  DEFAULT (N'Active') FOR [Status]
GO
ALTER TABLE [dbo].[KitchenWorkstation] ADD  CONSTRAINT [DF_KitchenWorkstation_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[KitchenWorkstation] ADD  CONSTRAINT [DF_KitchenWorkstation_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[KpiDailySnapshot] ADD  CONSTRAINT [DF_KpiDailySnapshot_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[KpiDailySnapshot] ADD  CONSTRAINT [DF_KpiDailySnapshot_CalculatedAt]  DEFAULT (sysdatetime()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[KpiDailySnapshot] ADD  CONSTRAINT [DF_KpiDailySnapshot_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[KpiDailySnapshot] ADD  CONSTRAINT [DF_KpiDailySnapshot_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[KpiMetricDefinition] ADD  CONSTRAINT [DF_KpiMetricDefinition_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[KpiMetricDefinition] ADD  CONSTRAINT [DF_KpiMetricDefinition_Version]  DEFAULT ((1)) FOR [VersionNo]
GO
ALTER TABLE [dbo].[KpiMetricDefinition] ADD  CONSTRAINT [DF_KpiMetricDefinition_Active]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[KpiMetricDefinition] ADD  CONSTRAINT [DF_KpiMetricDefinition_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[KpiMetricDefinition] ADD  CONSTRAINT [DF_KpiMetricDefinition_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[KpiMonthlySnapshot] ADD  CONSTRAINT [DF_KpiMonthlySnapshot_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[KpiMonthlySnapshot] ADD  CONSTRAINT [DF_KpiMonthlySnapshot_CalculatedAt]  DEFAULT (sysdatetime()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[KpiMonthlySnapshot] ADD  CONSTRAINT [DF_KpiMonthlySnapshot_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[KpiMonthlySnapshot] ADD  CONSTRAINT [DF_KpiMonthlySnapshot_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[LeaveRequest] ADD  CONSTRAINT [DF_LeaveRequest_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[LeaveRequest] ADD  CONSTRAINT [DF_LeaveRequest_Status]  DEFAULT (N'Pending') FOR [RequestStatus]
GO
ALTER TABLE [dbo].[LeaveRequest] ADD  CONSTRAINT [DF_LeaveRequest_SubmittedAt]  DEFAULT (sysdatetime()) FOR [SubmittedAt]
GO
ALTER TABLE [dbo].[LeaveRequest] ADD  CONSTRAINT [DF_LeaveRequest_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[LeaveRequest] ADD  CONSTRAINT [DF_LeaveRequest_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[LocationTracking] ADD  CONSTRAINT [DF_LocationTracking_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[LocationTracking] ADD  CONSTRAINT [DF_LocationTracking_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[LocationTracking] ADD  CONSTRAINT [DF_LocationTracking_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[MaterialConsumptionVariance] ADD  CONSTRAINT [DF_MaterialConsumptionVariances_StandardQuantity]  DEFAULT ((0)) FOR [StandardQuantity]
GO
ALTER TABLE [dbo].[MaterialConsumptionVariance] ADD  CONSTRAINT [DF_MaterialConsumptionVariances_ActualQuantity]  DEFAULT ((0)) FOR [ActualQuantity]
GO
ALTER TABLE [dbo].[MaterialConsumptionVariance] ADD  CONSTRAINT [DF_MaterialConsumptionVariances_VarianceQuantity]  DEFAULT ((0)) FOR [VarianceQuantity]
GO
ALTER TABLE [dbo].[MaterialIssueLine] ADD  CONSTRAINT [DF_MaterialIssueLines_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[MaterialRequirement] ADD  CONSTRAINT [DF_MaterialRequirements_RequiredQuantity]  DEFAULT ((0)) FOR [RequiredQuantity]
GO
ALTER TABLE [dbo].[MaterialRequirement] ADD  CONSTRAINT [DF_MaterialRequirements_AvailableQuantity]  DEFAULT ((0)) FOR [AvailableQuantity]
GO
ALTER TABLE [dbo].[MaterialRequirement] ADD  CONSTRAINT [DF_MaterialRequirements_ExpectedIncomingQuantity]  DEFAULT ((0)) FOR [ExpectedIncomingQuantity]
GO
ALTER TABLE [dbo].[MaterialRequirement] ADD  CONSTRAINT [DF_MaterialRequirements_PurchaseQuantity]  DEFAULT ((0)) FOR [PurchaseQuantity]
GO
ALTER TABLE [dbo].[MaterialRequirement] ADD  CONSTRAINT [DF_MaterialRequirements_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[MaterialReservation] ADD  CONSTRAINT [DF_MaterialReservations_ReservedQuantity]  DEFAULT ((0)) FOR [ReservedQuantity]
GO
ALTER TABLE [dbo].[MaterialReservation] ADD  CONSTRAINT [DF_MaterialReservations_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[MealCost] ADD  CONSTRAINT [DF_MealCosts_AcceptedMealQuantity]  DEFAULT ((0)) FOR [AcceptedMealQuantity]
GO
ALTER TABLE [dbo].[MealCost] ADD  CONSTRAINT [DF_MealCosts_TotalActualCost]  DEFAULT ((0)) FOR [TotalActualCost]
GO
ALTER TABLE [dbo].[MealCost] ADD  CONSTRAINT [DF_MealCosts_CalculatedAt]  DEFAULT (sysutcdatetime()) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[MealWasteRecord] ADD  CONSTRAINT [DF_MealWasteRecord_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[MealWasteRecord] ADD  CONSTRAINT [DF_MealWasteRecord_OccurredAt]  DEFAULT (sysdatetime()) FOR [OccurredAt]
GO
ALTER TABLE [dbo].[MealWasteRecord] ADD  CONSTRAINT [DF_MealWasteRecord_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[MealWasteRecord] ADD  CONSTRAINT [DF_MealWasteRecord_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[MedicalExamination] ADD  CONSTRAINT [DF_MedicalExamination_Id1]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[MedicalExamination] ADD  CONSTRAINT [DF_MedicalExamination_Price]  DEFAULT ((0)) FOR [Price]
GO
ALTER TABLE [dbo].[MedicalExamination] ADD  CONSTRAINT [DF_MedicalExamination_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[MedicalExamination] ADD  CONSTRAINT [DF_MedicalExamination_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_Quantity]  DEFAULT ((0)) FOR [Quantity]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_PriceInput]  DEFAULT ((0)) FOR [PriceInput]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_Price]  DEFAULT ((0)) FOR [Price]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_PriceTotal]  DEFAULT ((0)) FOR [PriceTotal]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_IsMain]  DEFAULT ((0)) FOR [IsMain]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[MedicineWarehouse] ADD  CONSTRAINT [DF_MedicineWarehouse_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[MenuChangeRequest] ADD  CONSTRAINT [DF_MenuChangeRequest_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[MenuChangeRequest] ADD  CONSTRAINT [DF_MenuChangeRequest_Status]  DEFAULT (N'Pending') FOR [RequestStatus]
GO
ALTER TABLE [dbo].[MenuChangeRequest] ADD  CONSTRAINT [DF_MenuChangeRequest_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[MenuChangeRequest] ADD  CONSTRAINT [DF_MenuChangeRequest_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[MenuLine] ADD  CONSTRAINT [DF_MenuLines_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_LevelShow]  DEFAULT ((1)) FOR [LevelShow]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_OrderShow]  DEFAULT ((1)) FOR [OrderShow]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_IsActive]  DEFAULT ((0)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ObjectMenu] ADD  CONSTRAINT [DF_ObjectMenu_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Order] ADD  CONSTRAINT [DF_Orders_DeliveryDate]  DEFAULT (sysutcdatetime()) FOR [DeliveryDate]
GO
ALTER TABLE [dbo].[OrderLine] ADD  CONSTRAINT [DF_OrderLines_LineAmount]  DEFAULT ((0)) FOR [LineAmount]
GO
ALTER TABLE [dbo].[OrderReconciliation] ADD  CONSTRAINT [DF_OrderReconciliations_ReconciliationDate]  DEFAULT (sysutcdatetime()) FOR [ReconciliationDate]
GO
ALTER TABLE [dbo].[OrderReconciliation] ADD  CONSTRAINT [DF_OrderReconciliations_OrderedQuantity]  DEFAULT ((0)) FOR [OrderedQuantity]
GO
ALTER TABLE [dbo].[OrderReconciliation] ADD  CONSTRAINT [DF_OrderReconciliations_DeliveredQuantity]  DEFAULT ((0)) FOR [DeliveredQuantity]
GO
ALTER TABLE [dbo].[OrderReconciliation] ADD  CONSTRAINT [DF_OrderReconciliations_AcceptedQuantity]  DEFAULT ((0)) FOR [AcceptedQuantity]
GO
ALTER TABLE [dbo].[OutboundMessage] ADD  CONSTRAINT [DF_OutboundMessage_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[OutboundMessage] ADD  CONSTRAINT [DF_OutboundMessage_QueuedAt]  DEFAULT (sysdatetime()) FOR [QueuedAt]
GO
ALTER TABLE [dbo].[OutboundMessage] ADD  CONSTRAINT [DF_OutboundMessage_Status]  DEFAULT (N'Queued') FOR [MessageStatus]
GO
ALTER TABLE [dbo].[OutboundMessage] ADD  CONSTRAINT [DF_OutboundMessage_Retry]  DEFAULT ((0)) FOR [RetryCount]
GO
ALTER TABLE [dbo].[OutboundMessage] ADD  CONSTRAINT [DF_OutboundMessage_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[OutboundMessage] ADD  CONSTRAINT [DF_OutboundMessage_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[PackagingLedger] ADD  CONSTRAINT [DF_PackagingLedger_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[PackagingLedger] ADD  CONSTRAINT [DF_PackagingLedger_At]  DEFAULT (sysdatetime()) FOR [TransactionAt]
GO
ALTER TABLE [dbo].[PackagingLedger] ADD  CONSTRAINT [DF_PackagingLedger_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[PackagingLedger] ADD  CONSTRAINT [DF_PackagingLedger_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[PackingOrder] ADD  CONSTRAINT [DF_PackingOrders_PackingDate]  DEFAULT (sysutcdatetime()) FOR [PackingDate]
GO
ALTER TABLE [dbo].[PackingResult] ADD  CONSTRAINT [DF_PackingResults_PackedQuantity]  DEFAULT ((0)) FOR [PackedQuantity]
GO
ALTER TABLE [dbo].[PackingResult] ADD  CONSTRAINT [DF_PackingResults_RejectedQuantity]  DEFAULT ((0)) FOR [RejectedQuantity]
GO
ALTER TABLE [dbo].[Patient] ADD  CONSTRAINT [DF_Patient_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[Patient] ADD  CONSTRAINT [DF_Patient_CouponCode]  DEFAULT ((1)) FOR [CouponCode]
GO
ALTER TABLE [dbo].[Patient] ADD  CONSTRAINT [DF_Patient_Visit]  DEFAULT ((1)) FOR [Visit]
GO
ALTER TABLE [dbo].[Patient] ADD  CONSTRAINT [DF_Patient_TotalPrice]  DEFAULT ((0)) FOR [TotalPrice]
GO
ALTER TABLE [dbo].[Patient] ADD  CONSTRAINT [DF_Patient_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Patient] ADD  CONSTRAINT [DF_Patient_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[PatientDetail] ADD  CONSTRAINT [DF_PatientDetail_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[PatientDetail] ADD  CONSTRAINT [DF_PatientDetail_Price]  DEFAULT ((0)) FOR [Price]
GO
ALTER TABLE [dbo].[PatientDetail] ADD  CONSTRAINT [DF_PatientDetail_Quantity]  DEFAULT ((1)) FOR [Quantity]
GO
ALTER TABLE [dbo].[PatientDetail] ADD  CONSTRAINT [DF_PatientDetail_TotalPrice]  DEFAULT ((0)) FOR [TotalPrice]
GO
ALTER TABLE [dbo].[PatientDetail] ADD  CONSTRAINT [DF_PatientDetail_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[PatientDetail] ADD  CONSTRAINT [DF_PatientDetail_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Person] ADD  CONSTRAINT [DF_Person_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[Person] ADD  CONSTRAINT [DF_Person_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Person] ADD  CONSTRAINT [DF_Person_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ProductionBatche] ADD  CONSTRAINT [DF_ProductionBatches_StartDate]  DEFAULT (sysutcdatetime()) FOR [StartDate]
GO
ALTER TABLE [dbo].[ProductionLine] ADD  CONSTRAINT [DF_ProductionLine_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ProductionLine] ADD  CONSTRAINT [DF_ProductionLine_Status]  DEFAULT (N'Active') FOR [Status]
GO
ALTER TABLE [dbo].[ProductionLine] ADD  CONSTRAINT [DF_ProductionLine_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ProductionLine] ADD  CONSTRAINT [DF_ProductionLine_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ProductionOrder] ADD  CONSTRAINT [DF_ProductionOrders_PlannedStartDate]  DEFAULT (sysutcdatetime()) FOR [PlannedStartDate]
GO
ALTER TABLE [dbo].[ProductionOutput] ADD  CONSTRAINT [DF_ProductionOutputs_DishId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [DishId]
GO
ALTER TABLE [dbo].[ProductionOutput] ADD  CONSTRAINT [DF_ProductionOutputs_ProducedQuantity]  DEFAULT ((0)) FOR [ProducedQuantity]
GO
ALTER TABLE [dbo].[ProductionOutput] ADD  CONSTRAINT [DF_ProductionOutputs_AcceptedQuantity]  DEFAULT ((0)) FOR [AcceptedQuantity]
GO
ALTER TABLE [dbo].[ProductionOutput] ADD  CONSTRAINT [DF_ProductionOutputs_RejectedQuantity]  DEFAULT ((0)) FOR [RejectedQuantity]
GO
ALTER TABLE [dbo].[ProductionPlan] ADD  CONSTRAINT [DF_ProductionPlans_PlanDate]  DEFAULT (sysutcdatetime()) FOR [PlanDate]
GO
ALTER TABLE [dbo].[ProductionPlanLine] ADD  CONSTRAINT [DF_ProductionPlanLines_PlannedQuantity]  DEFAULT ((0)) FOR [PlannedQuantity]
GO
ALTER TABLE [dbo].[ProofOfDeliverie] ADD  CONSTRAINT [DF_ProofOfDeliveries_ConfirmedAt]  DEFAULT (sysutcdatetime()) FOR [ConfirmedAt]
GO
ALTER TABLE [dbo].[PurchaseOrderLine] ADD  CONSTRAINT [DF_PurchaseOrderLines_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[PurchaseOrderLine] ADD  CONSTRAINT [DF_PurchaseOrderLines_LineAmount]  DEFAULT ((0)) FOR [LineAmount]
GO
ALTER TABLE [dbo].[PurchaseRequestLine] ADD  CONSTRAINT [DF_PurchaseRequestLines_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[Recipe] ADD  CONSTRAINT [DF_Recipes_YieldQuantity]  DEFAULT ((0)) FOR [YieldQuantity]
GO
ALTER TABLE [dbo].[Recipe] ADD  CONSTRAINT [DF_Recipes_IsActive]  DEFAULT ((0)) FOR [IsActive]
GO
ALTER TABLE [dbo].[RecipeLine] ADD  CONSTRAINT [DF_RecipeLines_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[RecipeLine] ADD  CONSTRAINT [DF_RecipeLines_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[ReturnInspection] ADD  CONSTRAINT [DF_ReturnInspections_InspectionDate]  DEFAULT (sysutcdatetime()) FOR [InspectionDate]
GO
ALTER TABLE [dbo].[ShiftAssignment] ADD  CONSTRAINT [DF_ShiftAssignment_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ShiftAssignment] ADD  CONSTRAINT [DF_ShiftAssignment_Status]  DEFAULT (N'Planned') FOR [AssignmentStatus]
GO
ALTER TABLE [dbo].[ShiftAssignment] ADD  CONSTRAINT [DF_ShiftAssignment_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ShiftAssignment] ADD  CONSTRAINT [DF_ShiftAssignment_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[ShiftSwapRequest] ADD  CONSTRAINT [DF_ShiftSwapRequest_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[ShiftSwapRequest] ADD  CONSTRAINT [DF_ShiftSwapRequest_Status]  DEFAULT (N'Pending') FOR [RequestStatus]
GO
ALTER TABLE [dbo].[ShiftSwapRequest] ADD  CONSTRAINT [DF_ShiftSwapRequest_SubmittedAt]  DEFAULT (sysdatetime()) FOR [SubmittedAt]
GO
ALTER TABLE [dbo].[ShiftSwapRequest] ADD  CONSTRAINT [DF_ShiftSwapRequest_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[ShiftSwapRequest] ADD  CONSTRAINT [DF_ShiftSwapRequest_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[StandardCost] ADD  CONSTRAINT [DF_StandardCosts_CostPerServing]  DEFAULT ((0)) FOR [CostPerServing]
GO
ALTER TABLE [dbo].[StockCountLine] ADD  CONSTRAINT [DF_StockCountLines_SystemQuantity]  DEFAULT ((0)) FOR [SystemQuantity]
GO
ALTER TABLE [dbo].[StockCountLine] ADD  CONSTRAINT [DF_StockCountLines_PhysicalQuantity]  DEFAULT ((0)) FOR [PhysicalQuantity]
GO
ALTER TABLE [dbo].[StockCountLine] ADD  CONSTRAINT [DF_StockCountLines_VarianceQuantity]  DEFAULT ((0)) FOR [VarianceQuantity]
GO
ALTER TABLE [dbo].[StockCountLine] ADD  CONSTRAINT [DF_StockCountLines_UnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [UnitId]
GO
ALTER TABLE [dbo].[StorageLocation] ADD  CONSTRAINT [DF_StorageLocations_WarehouseId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [WarehouseId]
GO
ALTER TABLE [dbo].[Supplier] ADD  CONSTRAINT [DF_Supplier_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[Supplier] ADD  CONSTRAINT [DF_Supplier_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Supplier] ADD  CONSTRAINT [DF_Supplier_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[SupplierIssue] ADD  CONSTRAINT [DF_SupplierIssue_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[SupplierIssue] ADD  CONSTRAINT [DF_SupplierIssue_Severity]  DEFAULT (N'Medium') FOR [Severity]
GO
ALTER TABLE [dbo].[SupplierIssue] ADD  CONSTRAINT [DF_SupplierIssue_ReportedAt]  DEFAULT (sysdatetime()) FOR [ReportedAt]
GO
ALTER TABLE [dbo].[SupplierIssue] ADD  CONSTRAINT [DF_SupplierIssue_Status]  DEFAULT (N'Open') FOR [IssueStatus]
GO
ALTER TABLE [dbo].[SupplierIssue] ADD  CONSTRAINT [DF_SupplierIssue_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[SupplierIssue] ADD  CONSTRAINT [DF_SupplierIssue_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[SupplierMaterial] ADD  CONSTRAINT [DF_SupplierMaterials_IsPreferred]  DEFAULT ((0)) FOR [IsPreferred]
GO
ALTER TABLE [dbo].[TemperatureLog] ADD  CONSTRAINT [DF_TemperatureLog_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[TemperatureLog] ADD  CONSTRAINT [DF_TemperatureLog_MeasuredAt]  DEFAULT (sysdatetime()) FOR [MeasuredAt]
GO
ALTER TABLE [dbo].[TemperatureLog] ADD  CONSTRAINT [DF_TemperatureLog_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[TemperatureLog] ADD  CONSTRAINT [DF_TemperatureLog_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[TestDemo] ADD  CONSTRAINT [DF_TestDemo_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[TestDemo] ADD  CONSTRAINT [DF_TestDemo_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[TestDemo] ADD  CONSTRAINT [DF_TestDemo_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UnitMeasure] ADD  CONSTRAINT [DF_UnitMeasure_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[UnitMeasure] ADD  CONSTRAINT [DF_UnitMeasure_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[UnitMeasure] ADD  CONSTRAINT [DF_UnitMeasure_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UomConversion] ADD  CONSTRAINT [DF_UomConversions_FromUnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [FromUnitId]
GO
ALTER TABLE [dbo].[UomConversion] ADD  CONSTRAINT [DF_UomConversions_ToUnitId]  DEFAULT ('00000000-0000-0000-0000-000000000000') FOR [ToUnitId]
GO
ALTER TABLE [dbo].[UomConversion] ADD  CONSTRAINT [DF_UomConversions_IsActive]  DEFAULT ((0)) FOR [IsActive]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsActive]  DEFAULT ((0)) FOR [IsActive]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsAdmin]  DEFAULT ((0)) FOR [IsAdmin]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[User] ADD  CONSTRAINT [DF_User_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UserClaim] ADD  CONSTRAINT [DF_UserClaim_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[UserClaim] ADD  CONSTRAINT [DF_UserClaim_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[UserClaim] ADD  CONSTRAINT [DF_UserClaim_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UserExternalIdentity] ADD  CONSTRAINT [DF_UserExternalIdentity_Id]  DEFAULT (newsequentialid()) FOR [Id]
GO
ALTER TABLE [dbo].[UserExternalIdentity] ADD  CONSTRAINT [DF_UserExternalIdentity_LinkedAt]  DEFAULT (sysdatetime()) FOR [LinkedAt]
GO
ALTER TABLE [dbo].[UserExternalIdentity] ADD  CONSTRAINT [DF_UserExternalIdentity_Status]  DEFAULT (N'Active') FOR [IdentityStatus]
GO
ALTER TABLE [dbo].[UserExternalIdentity] ADD  CONSTRAINT [DF_UserExternalIdentity_CreatedDate]  DEFAULT (sysdatetime()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[UserExternalIdentity] ADD  CONSTRAINT [DF_UserExternalIdentity_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UserGroup] ADD  CONSTRAINT [DF_UserGroup_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[UserGroup] ADD  CONSTRAINT [DF_UserGroup_IsDefault]  DEFAULT ((0)) FOR [IsDefault]
GO
ALTER TABLE [dbo].[UserGroup] ADD  CONSTRAINT [DF_UserGroup_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[UserGroup] ADD  CONSTRAINT [DF_UserGroup_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_Id]  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowAccess]  DEFAULT ((0)) FOR [AllowAccess]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowAdd]  DEFAULT ((0)) FOR [AllowAdd]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowEdit]  DEFAULT ((0)) FOR [AllowEdit]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowDelete]  DEFAULT ((0)) FOR [AllowDelete]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowPrint]  DEFAULT ((0)) FOR [AllowPrint]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowExport]  DEFAULT ((0)) FOR [AllowExport]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_AllowImport]  DEFAULT ((0)) FOR [AllowImport]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_CreatedDate]  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[UserRole] ADD  CONSTRAINT [DF_UserRole_IsDeleted]  DEFAULT ((0)) FOR [IsDeleted]
GO
