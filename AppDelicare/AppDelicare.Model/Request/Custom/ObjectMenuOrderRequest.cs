namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R: đổi cha / thứ tự của một menu (spec 3.4). Gửi danh sách trong body JSON.</summary>
    public class ObjectMenuOrderRequest
    {
        public Guid Id { get; set; }
        /// <summary>Menu cha; null = menu cấp 1.</summary>
        public Guid? ParentId { get; set; }
        public int OrderShow { get; set; }
    }
}
