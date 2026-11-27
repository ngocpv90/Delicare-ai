#nullable enable
using System.ComponentModel.DataAnnotations;
namespace AppDelicare.Model.Request
{
    /// <summary>RETAIL-1: sản phẩm bán lẻ. Code (G1 SPL-NNNNN) do máy chủ sinh – giá trị gửi lên bị bỏ qua. Kiểm nghiệp vụ ở RetailProductService.</summary>
    public class RetailProductRequest : BaseRequest
    {
        [MaxLength(50, ErrorMessage = "Mã sản phẩm tối đa 50 ký tự.")]
        public string? Code { get; set; }
        [MaxLength(255, ErrorMessage = "Tên món tối đa 255 ký tự.")]
        public string? Name { get; set; }
        [MaxLength(50, ErrorMessage = "Nhóm sản phẩm tối đa 50 ký tự.")]
        public string? CategoryCode { get; set; }
        /// <summary>Bữa bán – nhãn nối ';' (Sáng;Trưa;Tối).</summary>
        [MaxLength(100, ErrorMessage = "Bữa bán tối đa 100 ký tự.")]
        public string? MealTypes { get; set; }
        [MaxLength(50, ErrorMessage = "Đơn vị tối đa 50 ký tự.")]
        public string? Unit { get; set; }
        public decimal? Price { get; set; }
        [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
        public string? Description { get; set; }
        [MaxLength(255, ErrorMessage = "Nhãn tối đa 255 ký tự.")]
        public string? Tags { get; set; }
        [MaxLength(50, ErrorMessage = "Biểu tượng tối đa 50 ký tự.")]
        public string? Icon { get; set; }
        [MaxLength(500, ErrorMessage = "Đường dẫn ảnh tối đa 500 ký tự.")]
        public string? ImageUrl { get; set; }
        public int? SortOrder { get; set; }
        /// <summary>Active (Đang bán) / Inactive (Ngừng bán).</summary>
        [MaxLength(30, ErrorMessage = "Trạng thái tối đa 30 ký tự.")]
        public string? Status { get; set; }
    }
}
