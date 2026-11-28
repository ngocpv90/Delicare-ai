using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// PKG-FEE (anh duyệt 2026-11-28): danh mục Loại hộp / khay (bảng PackagingType) thay ReferenceLists:PackagingType khi có dữ liệu; phí thất thoát / cái theo loại
    /// (bảng kê dùng PackagingTypeCatalog.LossFee, không có dòng → BusinessRules:ContainerLossFee cũ); mã IN HOA, phí ≥ 0, mã trùng bị chặn; loại đã có giao dịch
    /// ở Sổ hộp khay không đổi mã / không xóa.
    /// </summary>
    public class PackagingTypeFeeTests
    {
        private static async Task<Data.Interface.IResult<bool>> SaveAsync(InMemoryApiFactory f, PackagingTypeRequest r, string type = ConstantHelper.TypeSave.Insert)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId));
            return await scope.ServiceProvider.GetRequiredService<IPackagingTypeService>().Save(r, type);
        }

        [Fact]
        public async Task Catalog_replaces_appsettings_fee_per_type_and_used_codes_are_locked()
        {
            using var f = new InMemoryApiFactory();
            f.Settings["BusinessRules:ContainerLossFee:THUNG_CU"] = "15000";
            f.CreateAuthorizedClient().Dispose();

            // Bảng trống → vẫn danh sách appsettings.
            var lists = f.Services.GetRequiredService<IOptions<ReferenceListsOptions>>().Value;
            (await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.Merge(db, lists).Lists["PackagingType"].Select(i => i.Code).ToList()))).Should().Contain("KHAY");

            (await SaveAsync(f, new PackagingTypeRequest { Code = "hop giu", Name = "Hộp" })).ResponseMessage.Should().Contain("IN HOA");
            (await SaveAsync(f, new PackagingTypeRequest { Code = "HOP_A", Name = "Hộp A", LossFee = -1 })).ResponseMessage.Should().Contain("không được âm");
            (await SaveAsync(f, new PackagingTypeRequest { Code = "hop_a", Name = "Hộp nhựa A", LossFee = 45000, SortOrder = 1 })).ResponseCode.Should().Be(200);
            (await SaveAsync(f, new PackagingTypeRequest { Code = "HOP_A", Name = "Trùng" })).ResponseCode.Should().NotBe(200, "mã trùng");
            (await SaveAsync(f, new PackagingTypeRequest { Code = "KHAY_CU", Name = "Khay cũ", LossFee = 20000, Status = "Inactive" })).ResponseCode.Should().Be(200);

            var merged = await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.Merge(db, lists).Lists["PackagingType"].Select(i => (i.Code, i.Label)).ToList()));
            merged.Should().Equal([("HOP_A", "Hộp nhựa A")], "bảng có dữ liệu → chỉ loại ĐANG DÙNG trong bảng (mã chuẩn hóa IN HOA)");
            var meta = f.Services.CreateScope().ServiceProvider.GetRequiredService<IMetaDataService>().GetReferenceLists("PackagingType").ListData!.Single();
            meta.ListItem.Select(i => i.Code).Should().Equal("HOP_A");

            var rules = f.Services.GetRequiredService<IOptions<BusinessRulesOptions>>().Value;
            (await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.LossFee(db, rules, "HOP_A")))).Should().Be(45000m);
            (await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.LossFee(db, rules, "KHAY_CU")))).Should().Be(20000m, "loại ngừng dùng vẫn tính phí cho số liệu cũ");
            (await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.LossFee(db, rules, "THUNG_CU")))).Should().Be(15000m, "không có trong bảng → cấu hình cũ");
            (await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.Label(db, lists, "KHAY_CU")))).Should().Be("Khay cũ");

            // Đã có giao dịch ở Sổ hộp khay → không đổi mã, không xóa; đổi phí / tên vẫn được.
            var row = await f.QueryAsync(db => Task.FromResult(db.PackagingType.Single(p => p.Code == "HOP_A")));
            await f.SeedAsync(db =>
            {
                db.PackagingLedger.Add(new PackagingLedger { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TransactionType = "OUT", PackagingType = "HOP_A", Quantity = 10 });
                return Task.CompletedTask;
            });
            (await SaveAsync(f, new PackagingTypeRequest { Id = row.Id, Code = "HOP_B", Name = "Hộp nhựa A" }, ConstantHelper.TypeSave.Update)).ResponseMessage.Should().Contain("không đổi mã");
            (await SaveAsync(f, new PackagingTypeRequest { Id = row.Id, Code = "HOP_A", Name = "Hộp nhựa A", LossFee = 50000 }, ConstantHelper.TypeSave.Update)).ResponseCode.Should().Be(200);
            (await f.QueryAsync(db => Task.FromResult(PackagingTypeCatalog.LossFee(db, rules, "HOP_A")))).Should().Be(50000m);
            (await SaveAsync(f, new PackagingTypeRequest { Id = row.Id }, ConstantHelper.TypeSave.Delete)).ResponseMessage.Should().Contain("Ngừng dùng");
        }
    }
}
