using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Inventory
{
    /// <summary>
    /// T16 (Pilot P4) – sổ kho theo khung cũ: gợi ý lô FEFO / FIFO, chặn lô hết hạn / QC không đạt / tạm giữ, cảnh báo lô nên xuất trước,
    /// phiếu xuất (Create → Post → Cancel = đảo), điều chỉnh / chuyển kho / tính lại tồn, kiểm kê (lệch &gt; 2% chỉ admin), chặn CRUD phá sổ.
    /// Hôm nay (đồng hồ test) = 08/12/2026.
    /// </summary>
    public class InventoryTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 8);
        private readonly InMemoryApiFactory _factory;

        public InventoryTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record LotSpec(string Name, int ExpiryInDays, int ReceivedDaysAgo, string? Qc, decimal Quantity);

        private sealed record Scenario(Guid Warehouse, Guid Warehouse2, Guid Unit, Guid Ingredient, Dictionary<string, Guid> Lots);

        /// <summary>Kho + kho 2 + đơn vị + nguyên liệu + các lô, mỗi lô nhập (RECEIPT) qua sổ cái. useFefo = null → không có chính sách (mặc định FEFO).</summary>
        private async Task<Scenario> SeedAsync(string code, bool? useFefo, params LotSpec[] lots)
        {
            Guid warehouse = Guid.NewGuid(), warehouse2 = Guid.NewGuid(), unit = Guid.NewGuid(), ingredient = Guid.NewGuid();
            var lotIds = lots.ToDictionary(l => l.Name, _ => Guid.NewGuid());
            await _factory.SeedAsync(async db =>
            {
                db.Warehouse.AddRange(
                    new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" },
                    new Warehouse { Id = warehouse2, Code = code + "-2", Name = "Kho 2 " + code, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "NL-" + code, Name = "Thịt " + code, BaseUnitId = unit, IsExpiryManaged = true });
                if (useFefo != null)
                {
                    db.InventoryPolicy.Add(new InventoryPolicy { Id = Guid.NewGuid(), IngredientId = ingredient, WarehouseId = warehouse, UseFefo = useFefo.Value, MinimumStock = 5, ReorderPoint = 20, Status = "Active" });
                }
                var ledger = new InventoryLedgerService(db);
                foreach (var l in lots)
                {
                    db.MaterialLot.Add(new MaterialLot
                    {
                        Id = lotIds[l.Name], IngredientId = ingredient, LotNumber = l.Name, LotNo = l.Name, ExpiryDate = Today.AddDays(l.ExpiryInDays),
                        ReceivedDate = Today.AddDays(-l.ReceivedDaysAgo), QcStatus = l.Qc, QualityStatus = l.Qc, Status = "Available", UnitCost = 1000,
                    });
                    await ledger.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = "RECEIPT", TransactionNumber = "NK-" + l.Name, WarehouseId = warehouse, IngredientId = ingredient,
                        MaterialLotId = lotIds[l.Name], UnitId = unit, QuantityIn = l.Quantity, UnitCost = 1000,
                    });
                }
            });
            return new Scenario(warehouse, warehouse2, unit, ingredient, lotIds);
        }

        private Task<decimal> OnHandAsync(Guid warehouse, Guid ingredient, Guid? lot = null)
            => _factory.QueryAsync(db => db.InventoryBalance.AsNoTracking()
                .Where(b => b.WarehouseId == warehouse && b.IngredientId == ingredient && (lot == null || b.MaterialLotId == lot))
                .SumAsync(b => b.OnHandQuantity));

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));
        private static async Task<LegacyResult> PutJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PutAsJsonAsync(url, body));

        private static List<JsonElement> List(LegacyResult r)
        {
            r.Code.Should().Be(200, r.Message);
            return [.. r.ListData.EnumerateArray()];
        }

        private static Task<LegacyResult> CreateIssue(HttpClient c, Scenario s, params object[] lines)
            => PostJson(c, "/api/MaterialIssue/Create", new { warehouseId = s.Warehouse, note = "[DEMO] xuất thử", listLine = lines });

        private static Guid IssueId(JsonElement detail) => detail.GetProperty("materialIssue").GetProperty("id").GetGuid();

        [Fact]
        public async Task Fefo_suggestion_skips_expired_failed_qc_and_held_lots()
        {
            var s = await SeedAsync("FEFO", useFefo: null,
                new LotSpec("L-A", 10, 9, "DAT", 50), new LotSpec("L-B", 3, 2, "PASS", 30), new LotSpec("L-C", -1, 20, "DAT", 40),
                new LotSpec("L-D", 1, 1, "KHONG_DAT", 25), new LotSpec("L-E", 2, 3, "DAT", 15), new LotSpec("L-F", 5, 1, "HOLD", 10));
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/MaterialLot/Hold", ("Id", s.Lots["L-E"]), ("Reason", "Nghi nhiễm bẩn"))).Ok();

            var suggestion = (await c.GetResultAsync($"/api/MaterialIssue/SuggestLots?warehouseId={s.Warehouse}&ingredientId={s.Ingredient}&quantity=60")).Ok();
            suggestion.GetProperty("method").GetString().Should().Be("FEFO", "không có chính sách → FEFO (D-24)");
            var picks = suggestion.GetProperty("listPick").EnumerateArray().Select(p => (p.GetProperty("lotNumber").GetString(), p.GetProperty("quantity").GetDecimal())).ToList();
            picks.Should().Equal(("L-B", 30m), ("L-A", 30m));
            suggestion.GetProperty("shortQuantity").GetDecimal().Should().Be(0);
            suggestion.GetProperty("listBlocked").EnumerateArray().Select(b => b.GetProperty("lotNumber").GetString())
                .Should().BeEquivalentTo(["L-C", "L-D", "L-E", "L-F"]);

            var short_ = (await c.GetResultAsync($"/api/MaterialIssue/SuggestLots?warehouseId={s.Warehouse}&ingredientId={s.Ingredient}&quantity=100")).Ok();
            short_.GetProperty("shortQuantity").GetDecimal().Should().Be(20, "chỉ 80 xuất được (L-A + L-B)");

            var byLot = List(await c.GetResultAsync($"/api/InventoryBalance/GetByLot?ingredientId={s.Ingredient}"));
            var lotC = byLot.Single(r => r.GetProperty("lotNumber").GetString() == "L-C");
            lotC.GetProperty("isExpired").GetBoolean().Should().BeTrue();
            lotC.GetProperty("blockReason").GetString().Should().Contain("hết hạn");
            byLot.Single(r => r.GetProperty("lotNumber").GetString() == "L-D").GetProperty("blockReason").GetString().Should().Contain("KHONG_DAT");
            byLot.Single(r => r.GetProperty("lotNumber").GetString() == "L-B").GetProperty("pickRank").GetInt32().Should().Be(1);

            var summary = List(await c.GetResultAsync($"/api/InventoryBalance/GetSummary?warehouseId={s.Warehouse}")).Single();
            summary.GetProperty("onHandQuantity").GetDecimal().Should().Be(170);
            summary.GetProperty("usableQuantity").GetDecimal().Should().Be(80);
            summary.GetProperty("expiredQuantity").GetDecimal().Should().Be(40);
            summary.GetProperty("expiringLotCount").GetInt32().Should().Be(4, "L-B, L-D, L-E, L-F hết hạn trong 7 ngày (BusinessRules:ExpiringLotDays)");
        }

        [Fact]
        public async Task Fifo_policy_picks_by_received_date_and_fefo_by_expiry()
        {
            var fifo = await SeedAsync("FIFO", useFefo: false, new LotSpec("X-OLD", 20, 10, "DAT", 10), new LotSpec("Y-NEW", 5, 2, "DAT", 10));
            var fefo = await SeedAsync("FEFO2", useFefo: true, new LotSpec("X-OLD2", 20, 10, "DAT", 10), new LotSpec("Y-NEW2", 5, 2, "DAT", 10));
            using var c = _factory.CreateAuthorizedClient();

            var s1 = (await c.GetResultAsync($"/api/MaterialIssue/SuggestLots?warehouseId={fifo.Warehouse}&ingredientId={fifo.Ingredient}&quantity=12")).Ok();
            s1.GetProperty("method").GetString().Should().Be("FIFO");
            s1.GetProperty("listPick").EnumerateArray().Select(p => p.GetProperty("lotNumber").GetString()).Should().Equal("X-OLD", "Y-NEW");

            var s2 = (await c.GetResultAsync($"/api/MaterialIssue/SuggestLots?warehouseId={fefo.Warehouse}&ingredientId={fefo.Ingredient}&quantity=12")).Ok();
            s2.GetProperty("method").GetString().Should().Be("FEFO");
            s2.GetProperty("listPick").EnumerateArray().Select(p => p.GetProperty("lotNumber").GetString()).Should().Equal("Y-NEW2", "X-OLD2");
        }

        [Fact]
        public async Task Issue_blocks_expired_and_failed_qc_lots_warns_on_later_lot_and_posts_out_entries()
        {
            var s = await SeedAsync("ISSUE", useFefo: null,
                new LotSpec("I-A", 10, 9, "DAT", 50), new LotSpec("I-B", 3, 2, "DAT", 30), new LotSpec("I-C", -1, 20, "DAT", 40), new LotSpec("I-D", 4, 1, "REJECT", 25));
            using var c = _factory.CreateAuthorizedClient();

            (await CreateIssue(c, s, new { ingredientId = s.Ingredient, materialLotId = s.Lots["I-C"], quantity = 5m })).Fail("hết hạn");
            (await CreateIssue(c, s, new { ingredientId = s.Ingredient, materialLotId = s.Lots["I-D"], quantity = 5m })).Fail("REJECT");
            (await CreateIssue(c, s, new { ingredientId = s.Ingredient, materialLotId = s.Lots["I-B"], quantity = 31m })).Fail("Không đủ tồn");
            (await CreateIssue(c, s, new { ingredientId = s.Ingredient, quantity = 81m })).Fail("Không đủ tồn");
            (await CreateIssue(c, s)).Fail("ít nhất 1 dòng");

            // Chọn lô hạn xa (I-A) khi còn I-B hạn gần hơn → cảnh báo, không chặn.
            var created = (await CreateIssue(c, s, new { ingredientId = s.Ingredient, materialLotId = s.Lots["I-A"], quantity = 10m })).Ok();
            created.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().ContainSingle(w => w!.Contains("I-B") && w.Contains("FEFO"));
            created.GetProperty("materialIssue").GetProperty("issueNumber").GetString().Should().MatchRegex(@"^PX-261208-\d{4}$");
            created.GetProperty("materialIssue").GetProperty("status").GetString().Should().Be("Draft");
            Guid id1 = IssueId(created);
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["I-A"])).Should().Be(50, "Nháp chưa ghi sổ");

            // Dòng không chọn lô → phân bổ FEFO: I-B 30 + I-A 5, không cảnh báo.
            var auto = (await CreateIssue(c, s, new { ingredientId = s.Ingredient, quantity = 35m })).Ok();
            auto.GetProperty("listWarning").GetArrayLength().Should().Be(0);
            auto.GetProperty("listLine").EnumerateArray().Select(l => (l.GetProperty("lotNumber").GetString(), l.GetProperty("quantity").GetDecimal()))
                .Should().BeEquivalentTo([("I-B", 30m), ("I-A", 5m)]);
            Guid id2 = IssueId(auto);

            var posted = (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", id2))).Ok();
            posted.GetProperty("materialIssue").GetProperty("status").GetString().Should().Be("Issued");
            var txns = posted.GetProperty("listTransaction").EnumerateArray().ToList();
            txns.Should().HaveCount(2);
            txns.Should().OnlyContain(t => t.GetProperty("transactionType").GetString() == "ISSUE" && t.GetProperty("quantityOut").GetDecimal() > 0
                && t.GetProperty("referenceType").GetString() == "MaterialIssue");
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["I-B"])).Should().Be(0);
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["I-A"])).Should().Be(45);
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", id2))).Fail("trạng thái");

            // Phiếu 1 (I-A 10) vẫn ghi được (còn 45); phiếu cần nhiều hơn tồn còn lại → chặn, không ghi bút toán nào.
            var big = (await CreateIssue(c, s, new { ingredientId = s.Ingredient, materialLotId = s.Lots["I-A"], quantity = 45m })).Ok();
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", id1))).Ok();
            int before = await _factory.QueryAsync(db => db.InventoryTransaction.CountAsync(t => t.IngredientId == s.Ingredient));
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", IssueId(big)))).Fail("Không đủ tồn");
            (await _factory.QueryAsync(db => db.InventoryTransaction.CountAsync(t => t.IngredientId == s.Ingredient))).Should().Be(before);
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["I-A"])).Should().Be(35);

            // Lô bị tạm giữ sau khi lập phiếu → không ghi sổ được.
            var held = (await CreateIssue(c, s, new { ingredientId = s.Ingredient, materialLotId = s.Lots["I-A"], quantity = 5m })).Ok();
            (await c.PostFormAsync("/api/MaterialLot/Hold", ("Id", s.Lots["I-A"]), ("Reason", "Chờ kiểm lại"))).Ok();
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", IssueId(held)))).Fail("tạm giữ");
            (await c.PostFormAsync("/api/MaterialLot/Release", ("Id", s.Lots["I-A"]))).Ok();
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", IssueId(held)))).Ok();
        }

        [Fact]
        public async Task Cancelling_an_issued_issue_reverses_entries_and_restores_balance()
        {
            var s = await SeedAsync("CANCEL", useFefo: null, new LotSpec("K-A", 10, 1, "DAT", 40));
            using var c = _factory.CreateAuthorizedClient();
            Guid id = IssueId((await CreateIssue(c, s, new { ingredientId = s.Ingredient, quantity = 15m })).Ok());
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", id))).Ok();
            (await OnHandAsync(s.Warehouse, s.Ingredient)).Should().Be(25);

            (await c.PostFormAsync("/api/MaterialIssue/Cancel", ("Id", id))).Fail("lý do");
            var cancelled = (await c.PostFormAsync("/api/MaterialIssue/Cancel", ("Id", id), ("Reason", "Bếp trả lại, chưa dùng"))).Ok();
            cancelled.GetProperty("materialIssue").GetProperty("status").GetString().Should().Be("Cancelled");
            var txns = cancelled.GetProperty("listTransaction").EnumerateArray().ToList();
            txns.Should().HaveCount(2);
            txns.Should().ContainSingle(t => t.GetProperty("referenceType").GetString() == "REVERSAL" && t.GetProperty("quantityIn").GetDecimal() == 15);
            (await OnHandAsync(s.Warehouse, s.Ingredient)).Should().Be(40);
            (await c.PostFormAsync("/api/MaterialIssue/Cancel", ("Id", id), ("Reason", "Lần 2"))).Fail("trạng thái");

            // Hủy phiếu nháp: không có bút toán.
            Guid draft = IssueId((await CreateIssue(c, s, new { ingredientId = s.Ingredient, quantity = 1m })).Ok());
            (await c.PostFormAsync("/api/MaterialIssue/Cancel", ("Id", draft), ("Reason", "Lập nhầm"))).Ok()
                .GetProperty("listTransaction").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task Balance_after_a_chain_of_adjust_transfer_issue_and_reversal_matches_the_ledger()
        {
            var s = await SeedAsync("CHAIN", useFefo: null, new LotSpec("C-A", 30, 1, "DAT", 100));
            using var c = _factory.CreateAuthorizedClient();
            Guid lot = s.Lots["C-A"];

            (await c.PostFormAsync("/api/InventoryTransaction/Adjust", ("WarehouseId", s.Warehouse), ("IngredientId", s.Ingredient), ("MaterialLotId", lot), ("Quantity", 10m))).Fail("Lý do");
            (await c.PostFormAsync("/api/InventoryTransaction/Adjust", ("WarehouseId", s.Warehouse), ("IngredientId", s.Ingredient), ("MaterialLotId", lot),
                ("Quantity", -200m), ("Reason", "Hủy hàng hỏng"))).Fail("Không đủ tồn");
            // BE2.3 (GAP-F-04): điều chỉnh tay → yêu cầu duyệt STOCK_ADJUSTMENT; chưa duyệt thì chưa có bút toán, duyệt xong mới ghi (ReferenceId = khóa nối).
            var pendingPlus = (await c.PostFormAsync("/api/InventoryTransaction/Adjust", ("WarehouseId", s.Warehouse), ("IngredientId", s.Ingredient), ("MaterialLotId", lot),
                ("Quantity", 10m), ("Reason", "Tồn đầu kỳ bổ sung"))).Ok();
            pendingPlus.GetProperty("pendingApproval").GetBoolean().Should().BeTrue();
            Guid plusRef = pendingPlus.GetProperty("referenceId").GetGuid();
            (await _factory.QueryAsync(db => db.InventoryTransaction.CountAsync(t => t.ReferenceId == plusRef))).Should().Be(0, "chưa duyệt");
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", pendingPlus.GetProperty("approvalRequestId").GetGuid()), ("Approve", true), ("Note", "ok"))).Ok();
            var plus = await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.ReferenceId == plusRef));
            plus.TransactionType.Should().Be("ADJUSTMENT");
            plus.TransactionNumber.Should().MatchRegex(@"^GD-261208-\d{4}$");
            plus.ReferenceType.Should().Be("ManualAdjustment");
            plus.Reason.Should().Be("Tồn đầu kỳ bổ sung");
            Guid minusRef = await c.AdjustAndApproveAsync(("WarehouseId", s.Warehouse), ("IngredientId", s.Ingredient), ("MaterialLotId", lot),
                ("Quantity", -5m), ("Reason", "Hỏng khi bảo quản"));
            (await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.ReferenceId == minusRef))).QuantityOut.Should().Be(5);

            var transfer = await c.PostFormAsync("/api/InventoryTransaction/Transfer", ("FromWarehouseId", s.Warehouse), ("ToWarehouseId", s.Warehouse2),
                ("IngredientId", s.Ingredient), ("MaterialLotId", lot), ("Quantity", 25m), ("Reason", "Chuyển bếp 2"));
            var legs = List(transfer);
            legs.Select(l => l.GetProperty("transactionType").GetString()).Should().Equal("TRANSFER_OUT", "TRANSFER_IN");
            legs[0].GetProperty("referenceId").GetGuid().Should().Be(legs[1].GetProperty("referenceId").GetGuid());
            (await c.PostFormAsync("/api/InventoryTransaction/Transfer", ("FromWarehouseId", s.Warehouse), ("ToWarehouseId", s.Warehouse),
                ("IngredientId", s.Ingredient), ("MaterialLotId", lot), ("Quantity", 1m), ("Reason", "Cùng chỗ"))).Fail("phải khác");

            Guid issue = IssueId((await CreateIssue(c, s, new { ingredientId = s.Ingredient, quantity = 30m })).Ok());
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", issue))).Ok();
            (await c.PostFormAsync("/api/InventoryTransaction/Reverse", ("Id", await _factory.QueryAsync(db => db.InventoryTransaction.Where(t => t.ReferenceId == minusRef).Select(t => t.Id).SingleAsync())), ("Reason", "Ghi nhầm hỏng"))).Ok();

            // 100 + 10 − 5 − 25 − 30 + 5 = 55 ở kho 1; 25 ở kho 2.
            (await OnHandAsync(s.Warehouse, s.Ingredient)).Should().Be(55);
            (await OnHandAsync(s.Warehouse2, s.Ingredient)).Should().Be(25);
            decimal ledger = await _factory.QueryAsync(db => db.InventoryTransaction.Where(t => t.WarehouseId == s.Warehouse && t.IngredientId == s.Ingredient)
                .SumAsync(t => t.QuantityIn - t.QuantityOut));
            ledger.Should().Be(55);

            // Tính lại từ sổ: khớp thì không đổi; tồn bị sửa lệch ngoài sổ → đưa về đúng.
            var rebuilt = (await c.PostFormAsync("/api/InventoryBalance/RebuildBalance", ("WarehouseId", s.Warehouse))).Ok();
            rebuilt.GetProperty("changed").GetInt32().Should().Be(0);
            await _factory.SeedAsync(async db =>
            {
                var b = await db.InventoryBalance.SingleAsync(x => x.WarehouseId == s.Warehouse && x.IngredientId == s.Ingredient);
                b.OnHandQuantity = 999;
            });
            (await c.PostFormAsync("/api/InventoryBalance/RebuildBalance", ("WarehouseId", s.Warehouse))).Ok().GetProperty("changed").GetInt32().Should().Be(1);
            (await OnHandAsync(s.Warehouse, s.Ingredient)).Should().Be(55);
        }

        /// <summary>User thường có quyền Xem / Thêm / Sửa trên scm-stockcount (không phải admin).</summary>
        private async Task<Guid> CreateStockKeeperAsync()
        {
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/scm-stockcount");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Kiểm kê", MetaLink = "#/scm-stockcount", IsActive = true, LevelShow = 2 };
                    db.ObjectMenu.Add(menu);
                }
                db.User.Add(new User { Id = userId, UserName = "kho-" + userId.ToString("N")[..8], IsActive = true, IsAdmin = false });
                db.UserGroup.Add(new UserGroup { Id = groupId, Name = "Thủ kho " + groupId.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = groupId, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
            });
            return userId;
        }

        [Fact]
        public async Task Stock_count_records_variance_posts_adjustments_and_needs_two_level_approval_above_threshold()
        {
            var s = await SeedAsync("COUNT", useFefo: null, new LotSpec("S-A", 30, 1, "DAT", 100), new LotSpec("S-B", 30, 1, "DAT", 50));
            using var admin = _factory.CreateAuthorizedClient();
            using var keeper = _factory.CreateAuthorizedClient(await CreateStockKeeperAsync());

            (await keeper.PostFormAsync("/api/StockCount/Create", ("WarehouseId", s.Warehouse), ("CountType", "YEARLY"))).Fail("DAILY");
            var created = (await keeper.PostFormAsync("/api/StockCount/Create", ("WarehouseId", s.Warehouse), ("CountType", "DAILY"))).Ok();
            Guid id = created.GetProperty("stockCount").GetProperty("id").GetGuid();
            created.GetProperty("stockCount").GetProperty("countNumber").GetString().Should().MatchRegex(@"^KK-261208-\d{4}$");
            var lines = created.GetProperty("listLine").EnumerateArray().ToList();
            lines.Should().HaveCount(2);
            Guid lineA = lines.Single(l => l.GetProperty("lotNumber").GetString() == "S-A").GetProperty("id").GetGuid();
            Guid lineB = lines.Single(l => l.GetProperty("lotNumber").GetString() == "S-B").GetProperty("id").GetGuid();
            lines.Should().OnlyContain(l => l.GetProperty("varianceQuantity").GetDecimal() == 0);

            // S-A thiếu 1 (1%), S-B thiếu 10 (20%) → vượt 2%: thủ kho không ghi sổ được.
            var recorded = (await PutJson(keeper, "/api/StockCount/RecordCounts", new
            {
                id, listLine = new object[] { new { id = lineA, physicalQuantity = 99m, varianceReason = "Hao hụt tự nhiên" }, new { id = lineB, physicalQuantity = 40m } },
            })).Ok();
            recorded.GetProperty("stockCount").GetProperty("status").GetString().Should().Be("Counting");
            recorded.GetProperty("maxVariancePercent").GetDecimal().Should().Be(20);
            recorded.GetProperty("needsApproval").GetBoolean().Should().BeTrue();
            recorded.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == lineB).GetProperty("varianceValue").GetDecimal().Should().Be(-10000);

            (await keeper.PostFormAsync("/api/StockCount/Post", ("Id", id))).Fail("lý do chênh lệch");
            (await PutJson(keeper, "/api/StockCount/RecordCounts", new { id, listLine = new object[] { new { id = lineB, physicalQuantity = 40m, varianceReason = "Mất mát" } } })).Ok();
            // BE2.3 (spec 7.8 submit / post): vượt 2% → phải gửi duyệt STOCK_COUNT 2 cấp; admin cũng không ghi sổ thẳng nữa.
            (await keeper.PostFormAsync("/api/StockCount/Post", ("Id", id))).Fail("gửi duyệt");
            (await admin.PostFormAsync("/api/StockCount/Post", ("Id", id))).Fail("gửi duyệt");
            var submitted = (await keeper.PostFormAsync("/api/StockCount/Submit", ("Id", id))).Ok();
            submitted.GetProperty("stockCount").GetProperty("status").GetString().Should().Be("PendingApproval");
            submitted.GetProperty("pendingApproval").GetProperty("stepCount").GetInt32().Should().Be(2);
            submitted.GetProperty("pendingApproval").GetProperty("screenId").GetString().Should().Be("scm-stockcount");
            (await PutJson(keeper, "/api/StockCount/RecordCounts", new { id, listLine = new object[] { new { id = lineA, physicalQuantity = 1m } } })).Fail("trạng thái");
            (await admin.PostFormAsync("/api/StockCount/Post", ("Id", id))).Fail("chờ duyệt");
            (await keeper.PostFormAsync("/api/StockCount/Approve", ("Id", id))).Fail("không có quyền");
            (await admin.PostFormAsync("/api/StockCount/Approve", ("Id", id), ("Note", "Cấp 1 ok"))).Ok();
            (await admin.GetResultAsync($"/api/StockCount/GetDetail?id={id}")).Ok().GetProperty("pendingApproval").GetProperty("screenId").GetString().Should().Be("scm-stockcount-l2");
            (await admin.PostFormAsync("/api/StockCount/Post", ("Id", id))).Fail("chờ duyệt");
            (await OnHandAsync(s.Warehouse, s.Ingredient)).Should().Be(150, "chưa ghi sổ");
            (await admin.PostFormAsync("/api/StockCount/Approve", ("Id", id), ("Note", "Kế toán ok"))).Ok();
            var approved = (await keeper.GetResultAsync($"/api/StockCount/GetDetail?id={id}")).Ok();
            approved.GetProperty("isApproved").GetBoolean().Should().BeTrue();
            approved.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().Equal("post");

            var posted = (await keeper.PostFormAsync("/api/StockCount/Post", ("Id", id))).Ok();
            posted.GetProperty("stockCount").GetProperty("status").GetString().Should().Be("Posted");
            posted.GetProperty("stockCount").GetProperty("approvedBy").GetGuid().Should().Be(InMemoryApiFactory.UserId, "người duyệt cấp cuối");
            var txns = posted.GetProperty("listTransaction").EnumerateArray().ToList();
            txns.Should().HaveCount(2);
            txns.Should().OnlyContain(t => t.GetProperty("transactionType").GetString() == "ADJUSTMENT" && t.GetProperty("referenceType").GetString() == "StockCount");
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["S-A"])).Should().Be(99);
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["S-B"])).Should().Be(40);
            (await PutJson(keeper, "/api/StockCount/RecordCounts", new { id, listLine = new object[] { new { id = lineA, physicalQuantity = 1m } } })).Fail("trạng thái");

            // Lệch nhỏ (≤ 2%) + hàng tìm thấy ngoài sổ là lô đã có (dòng mới): thủ kho tự ghi sổ được.
            Guid id2 = (await keeper.PostFormAsync("/api/StockCount/Create", ("WarehouseId", s.Warehouse))).Ok().GetProperty("stockCount").GetProperty("id").GetGuid();
            var small = (await PutJson(keeper, "/api/StockCount/RecordCounts", new
            {
                id = id2, listLine = new object[] { new { ingredientId = s.Ingredient, materialLotId = s.Lots["S-A"], physicalQuantity = 100m, varianceReason = "Đếm sót lần trước" } },
            })).Ok();
            small.GetProperty("stockCount").GetProperty("countType").GetString().Should().Be("ADHOC");
            small.GetProperty("maxVariancePercent").GetDecimal().Should().BeApproximately(1.01m, 0.01m);
            (await keeper.PostFormAsync("/api/StockCount/Submit", ("Id", id2))).Fail("không cần duyệt");
            (await keeper.PostFormAsync("/api/StockCount/Post", ("Id", id2))).Ok();
            (await OnHandAsync(s.Warehouse, s.Ingredient, s.Lots["S-A"])).Should().Be(100);
        }

        [Fact]
        public async Task Legacy_crud_cannot_break_the_ledger_or_lot_rules()
        {
            var s = await SeedAsync("CRUD", useFefo: null, new LotSpec("R-A", 10, 1, "KHONG_DAT", 20));
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/InventoryTransaction/Insert", ("WarehouseId", s.Warehouse), ("IngredientId", s.Ingredient), ("UnitId", s.Unit),
                ("QuantityIn", 5m), ("TransactionType", "RECEIPT"))).Fail("chỉ ghi thêm");
            (await c.PostFormAsync("/api/InventoryBalance/Insert", ("WarehouseId", s.Warehouse), ("IngredientId", s.Ingredient), ("UnitId", s.Unit))).Fail("sổ cái");
            (await c.PostFormAsync("/api/MaterialIssue/Insert", ("WarehouseId", s.Warehouse), ("IssueDate", Today))).Fail("MaterialIssue/Create");
            (await c.PostFormAsync("/api/MaterialIssueLine/Insert", ("MaterialIssueId", Guid.NewGuid()), ("IngredientId", s.Ingredient), ("Quantity", 1m))).Fail("MaterialIssue/Create");
            (await c.PostFormAsync("/api/StockCount/Insert", ("WarehouseId", s.Warehouse), ("CountDate", Today))).Fail("StockCount/Create");
            (await c.PostFormAsync("/api/StockCountLine/Insert", ("StockCountId", Guid.NewGuid()), ("IngredientId", s.Ingredient))).Fail("RecordCounts");

            // Lô QC KHONG_DAT không "sửa" thành đạt qua danh mục; lô có giao dịch không xóa được.
            (await c.PutFormAsync("/api/MaterialLot/Update", ("Id", s.Lots["R-A"]), ("IngredientId", s.Ingredient), ("LotNumber", "R-A"), ("QcStatus", "DAT"))).Fail("KHONG_DAT");
            (await c.DeleteResultAsync($"/api/MaterialLot/Delete?id={s.Lots["R-A"]}")).Fail();

            // Thêm lô tay: ghi song song cột cũ, trạng thái Available; sửa không đổi trạng thái.
            // MA-1 (D-63 c): số lô luôn do máy chủ sinh (số gửi lên bị bỏ qua); không có NCC → hậu tố TDK (tồn đầu kỳ).
            (await c.PostFormAsync("/api/MaterialLot/Insert", ("IngredientId", s.Ingredient), ("LotNumber", "TAY-01"), ("QcStatus", "DAT"),
                ("Status", "Consumed"), ("ExpiryDate", Today.AddDays(5)), ("Note", "[DEMO] TAY-01"))).Ok();
            var lot = await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().SingleAsync(l => l.Note == "[DEMO] TAY-01"));
            lot.LotNumber.Should().MatchRegex(@"^LO-\d{6}-TDK-\d{2}$");
            lot.LotNo.Should().Be(lot.LotNumber);
            lot.QualityStatus.Should().Be("DAT");
            lot.Status.Should().Be("Available");
            (await c.PutFormAsync("/api/MaterialLot/Update", ("Id", lot.Id), ("IngredientId", s.Ingredient), ("LotNumber", "DOI-SO"), ("QcStatus", "DAT"), ("Status", "Expired"))).Ok();
            var after = await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().SingleAsync(l => l.Id == lot.Id));
            after.Status.Should().Be("Available");
            after.LotNumber.Should().Be(lot.LotNumber, "MA-1: số lô không đổi sau khi cấp");
            (await c.PostFormAsync("/api/MaterialLot/Release", ("Id", lot.Id))).Fail("tạm giữ");
            (await c.PostFormAsync("/api/MaterialLot/Hold", ("Id", lot.Id))).Fail("Lý do");
        }
    }
}
