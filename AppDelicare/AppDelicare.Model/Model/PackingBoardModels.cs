namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.34 (D-115): bảng "Chia suất & đóng gói" – dòng = điểm giao (đơn), cột = món của ca (PackingOrder/GetBoard).</summary>
    public class PackingBoardModel
    {
        public DateTime Date { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftName { get; set; }
        public List<PackingBoardShiftModel> ListShift { get; set; } = [];
        public int DishesReady { get; set; }
        public int PointsComplete { get; set; }
        public int PointsTotal { get; set; }
        public decimal PortionsPacked { get; set; }
        public decimal PortionsTotal { get; set; }
        public decimal DamagedTotal { get; set; }
        public List<PackingBoardDishModel> ListDish { get; set; } = [];
        public List<PackingBoardRowModel> ListRow { get; set; } = [];
    }

    public class PackingBoardShiftModel
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
    }

    public class PackingBoardDishModel
    {
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public int BatchesPassed { get; set; }
        public int BatchesTotal { get; set; }
        public bool Ready { get; set; }
        public bool AllPacked { get; set; }
    }

    public class PackingBoardRowModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public Guid? DeliveryRouteId { get; set; }
        public string? RouteName { get; set; }
        public int DishesDone { get; set; }
        public int DishesTotal { get; set; }
        public bool Complete { get; set; }
        public List<PackingBoardCellModel> ListCell { get; set; } = [];
    }

    public class PackingBoardCellModel
    {
        public Guid DishId { get; set; }
        /// <summary>NA | WAITING | READY | PACKED | DAMAGED.</summary>
        public string Status { get; set; } = "NA";
        public decimal Quantity { get; set; }
        public decimal Damaged { get; set; }
    }

    /// <summary>UI-6.34: ghi kết quả chia cho nhiều ô (điểm giao × món) – mỗi ô ghi mọi lệnh theo mẻ, mặc định đủ số.</summary>
    public class PackingResultManyRequest
    {
        public DateTime Date { get; set; }
        public Guid? ShiftId { get; set; }
        public List<PackingResultCellInput> ListCell { get; set; } = [];
    }

    public class PackingResultCellInput
    {
        public Guid OrderId { get; set; }
        public Guid DishId { get; set; }
        public decimal? Damaged { get; set; }
        public string? Reason { get; set; }
    }

    public class PackingResultManyModel
    {
        public int Cells { get; set; }
        public int Orders { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }
}
