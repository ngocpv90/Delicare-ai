namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.32 (D-113): "Đạt tất cả & kết luận" – ghi Đạt mọi bước còn lại + kết luận Đạt trong một giao dịch.</summary>
    public class CleaningPassAllRequest
    {
        public Guid Id { get; set; }
        /// <summary>Hóa chất cho bước khử khuẩn (bắt buộc khi bước đó chưa Đạt).</summary>
        public string? ChemicalUsed { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>UI-6.32: bảng 3 cột Chờ kiểm tra → Đang vệ sinh → Hoàn tất (CleaningBatch/GetDayBoard).</summary>
    public class CleaningDayBoardModel
    {
        public DateTime Date { get; set; }
        public int WaitingReturns { get; set; }
        public int WaitingContainers { get; set; }
        public int InProgress { get; set; }
        public int InProgressContainers { get; set; }
        public int DonePass { get; set; }
        public int DoneTotal { get; set; }
        public int DoneReject { get; set; }
        public decimal? PassRate7d { get; set; }
        public List<CleaningWaitingCardModel> ListWaiting { get; set; } = [];
        public List<CleaningBatchCardModel> ListInProgress { get; set; } = [];
        public List<CleaningBatchCardModel> ListDone { get; set; } = [];
    }

    public class CleaningWaitingCardModel
    {
        public Guid ContainerReturnId { get; set; }
        public string? CustomerName { get; set; }
        public string? PackagingType { get; set; }
        public string? DeliveryNumber { get; set; }
        public DateTime ReturnDate { get; set; }
        public int ExpectedQuantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public int DamagedQuantity { get; set; }
        public int MissingQuantity { get; set; }
        /// <summary>Gợi ý kết quả kiểm tra: SHORTAGE khi thu về &lt; phải thu, DAMAGED khi có hỏng, còn lại OK.</summary>
        public string SuggestedResult { get; set; } = "OK";
    }

    public class CleaningBatchCardModel
    {
        public Guid Id { get; set; }
        public string? Number { get; set; }
        public string? CustomerName { get; set; }
        public string? PackagingType { get; set; }
        public int ContainerQuantity { get; set; }
        public int StepCount { get; set; }
        public int PassedStepCount { get; set; }
        public bool HasFailedStep { get; set; }
        public string? ResponsibleName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? QcResult { get; set; }
        public string? RejectReason { get; set; }
        public Guid ReturnInspectionId { get; set; }
        /// <summary>Mẻ Không đạt đã có mẻ vệ sinh lại (cùng phiếu kiểm tra, lập sau).</summary>
        public bool Redone { get; set; }
    }

    /// <summary>UI-6.32: khay sạch cho ngày mai (PackagingLedger/GetCleanStock).</summary>
    public class CleanStockModel
    {
        public DateTime Date { get; set; }
        public DateTime ForDate { get; set; }
        public List<CleanStockTypeModel> ListType { get; set; } = [];
    }

    public class CleanStockTypeModel
    {
        public string PackagingType { get; set; } = string.Empty;
        public string? Label { get; set; }
        /// <summary>Khay sạch sẵn sàng theo sổ bao bì (Σ sẵn sàng dùng lại − Σ giao khách, không âm).</summary>
        public decimal Ready { get; set; }
        public decimal Cleaning { get; set; }
        public decimal WaitingInspection { get; set; }
        /// <summary>Suất đã chốt ngày mai × khay / suất.</summary>
        public decimal Needed { get; set; }
        public decimal Shortage { get; set; }
    }

    public class CleaningBookRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Keyword { get; set; }
        public Guid? CustomerId { get; set; }
        /// <summary>PASS | REJECT.</summary>
        public string? Result { get; set; }
    }

    public class CleaningBookModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Batches { get; set; }
        public int Pass { get; set; }
        public int Reject { get; set; }
        public decimal Containers { get; set; }
        public List<CleaningBookDayModel> ListDay { get; set; } = [];
    }

    public class CleaningBookDayModel
    {
        public DateTime Date { get; set; }
        public int Batches { get; set; }
        public int Reject { get; set; }
        public List<CleaningBookRowModel> ListRow { get; set; } = [];
    }

    public class CleaningBookRowModel
    {
        public Guid Id { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Number { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? PackagingType { get; set; }
        public int ContainerQuantity { get; set; }
        public string? QcResult { get; set; }
        public string? Chemical { get; set; }
        public string? People { get; set; }
        public string? CorrectiveAction { get; set; }
    }
}
