using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// RETAIL-1 §8 (ảnh sản phẩm): ảnh tải lên bằng API tệp dùng chung (<c>DocumentFile/Upload</c>, Resource <c>RetailProduct</c>, nhóm <c>photo</c>, quyền Sửa màn Sản phẩm bán lẻ);
    /// <see cref="SetImage"/> gắn ảnh làm ảnh đại diện (ghi <c>RetailProduct.ImageUrl</c> = đường dẫn công khai); trang khách tải ảnh qua <see cref="GetPublicImage"/>
    /// – chỉ ảnh jpg / png của sản phẩm đang bán, không lộ tệp khác. Không bảng / cột mới.
    /// </summary>
    public interface IRetailProductImageService
    {
        Task<IResult<string>> SetImage(Guid productId, Guid documentFileId);
        Task<FileContentModel?> GetPublicImage(Guid productId);
    }

    public sealed class RetailProductImageService(IUnitOfWork unitOfWork, ICurrentActor currentActor, IClock clock, IOptions<DocumentStorageOptions> storage) : IRetailProductImageService
    {
        public const string Resource = "RetailProduct";
        public const string Category = "photo";
        private static readonly string[] ImageExt = [".jpg", ".jpeg", ".png"];
        private string Root => Path.GetFullPath(string.IsNullOrWhiteSpace(storage.Value.RootPath) ? "App_Data/DocumentFiles" : storage.Value.RootPath);

        public static string PublicUrl(Guid productId, Guid fileId) => $"/api/RetailPortal/ProductImage?id={productId}&v={fileId.ToString("N")[..8]}";

        public async Task<IResult<string>> SetImage(Guid productId, Guid documentFileId)
        {
            var actor = currentActor.RequireActor();
            var db = unitOfWork.DbContext;
            var product = await db.RetailProduct.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return Result<string>.Fail("Không tìm thấy sản phẩm");
            }
            var file = await db.DocumentFile.AsNoTracking().FirstOrDefaultAsync(f => f.Id == documentFileId && !f.IsDeleted);
            var key = DocumentStorageRules.Parse(file?.FileNameHas);
            if (file == null || key == null || key.Resource != Resource || key.RecordId != productId || key.Category != Category || !ImageExt.Contains(key.Extension.ToLowerInvariant()))
            {
                return Result<string>.Fail("Ảnh phải là tệp jpg / png tải lên cho chính sản phẩm này (nhóm Ảnh)");
            }
            product.ImageUrl = PublicUrl(productId, documentFileId);
            product.ModifiedDate = clock.Now;
            product.ModifiedBy = actor.UserId;
            await db.SaveChangesAsync();
            return Result<string>.Success(product.ImageUrl, "Đã đặt ảnh sản phẩm – trang khách hiện ảnh mới");
        }

        public async Task<FileContentModel?> GetPublicImage(Guid productId)
        {
            var db = unitOfWork.DbContext;
            var product = await db.RetailProduct.AsNoTracking().Where(p => p.Id == productId && !p.IsDeleted && p.Status == RetailCodes.ProductActive).Select(p => new { p.ImageUrl }).FirstOrDefaultAsync();
            if (product?.ImageUrl == null)
            {
                return null;
            }
            string prefix = $"{Resource}/{productId:N}/{Category}/";
            var files = await db.DocumentFile.AsNoTracking().Where(f => !f.IsDeleted && f.FileNameHas != null && f.FileNameHas.StartsWith(prefix)).Select(f => new { f.Id, f.FileNameHas }).ToListAsync();
            var chosen = files.FirstOrDefault(f => product.ImageUrl.Contains("v=" + f.Id.ToString("N")[..8], StringComparison.Ordinal));
            var key = DocumentStorageRules.Parse(chosen?.FileNameHas);
            if (chosen == null || key == null || !ImageExt.Contains(key.Extension.ToLowerInvariant()))
            {
                return null;
            }
            string path = DocumentStorageRules.FullPath(Root, chosen.FileNameHas!);
            if (!File.Exists(path))
            {
                return null;
            }
            return new FileContentModel
            {
                Content = await File.ReadAllBytesAsync(path), ContentType = key.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg",
                FileName = $"san-pham{key.Extension}",
            };
        }
    }
}
