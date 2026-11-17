# Bàn giao frontend – mục lục

Backend viết theo **khung code cũ** (D-30): route `api/<Controller>/<Action>`, kết quả `Result<T>` `{ responseCode, responseMessage, data }` / `Results<T>` `{ listData, totalRecords }`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400`. Quy ước chung: `P0-P2.md` mục 1. Frontend Angular theo khung Angular cũ (CLAUDE.md 4.4, D-35); làm FE **sau** khi xong backend Pilot (D-40).

| File | Màn hình (screen-id mockup) | Nhóm FE | Task backend | File thử API |
|---|---|---|---|---|
| [P0-P2.md](P0-P2.md) | Quản trị (`adm-*`), danh mục P1 (khách, NCC, NL, kho, đơn vị…), món / công thức / giá P2; mục 3 nhập Excel; **mục 4 hộp duyệt 1 cấp `wf-inbox`**; mục 6 người dùng 1 bước + mật khẩu tạm | FE-1, FE-2, FE-3, **FE-P2** (mục 4) | T45R, T45B.5, T49, T12, T45R.8 | `r1.0-foundation.http`, `r1.1-admin-masterdata.http`, `t45r8-admin-users.http` |
| [P3.md](P3.md) | Thực đơn tuần (`menu-menu`), phân công thực đơn, đơn hàng (`crm-order`), duyệt lệch suất | FE-P3 | T13 + T46B.1 | `p3-menu-order.http` |
| [P4.md](P4.md) | Sổ kho, tồn, lô (`scm-lot`), phiếu xuất FEFO, điều chỉnh / chuyển kho, kiểm kê | FE-P4 | T16 | `p4-inventory.http` |
| [P5.md](P5.md) | Nhu cầu NL / MRP (`scm-requirement`), đề nghị mua (`scm-pr`), PO + duyệt (Giám đốc ≥ 50 triệu), nhận hàng (`scm-gr`), QC đầu vào (`qc-inspection`), in Phiếu trước SX / Đơn đặt hàng | FE-P5 (gồm Pilot P5 + P6) | T14, T14.1, T15, T15.1 | `p5-mrp.http`, `p6-purchasing.http` |
| [P7.md](P7.md) | Kế hoạch SX (`prod-plan`, phát hành theo ca), lệnh SX, mẻ (`prod-batch`: xuất NL, nhiệt độ lõi, lưu mẫu, sản lượng, QC, đóng gói) | FE-P7 | T17, T17.1 | `p7-production.http` |
| [P8.md](P8.md) | Chuyến giao (`log-route`), điểm dừng / POD, đối soát suất (`crm-reconciliation`), phản ánh (`log-complaint`) | FE-P8 | T19 | `p8-delivery.http` |
| [BE2.1.md](BE2.1.md) | Đóng gói (`log-packing-order`), xếp xe + lập chuyến nhiều xe (`log-route`), đơn giao tay (`log-delivery-order`), thực hiện giao (`log-execution`), sinh kế hoạch theo ca (`prod-plan`), đề nghị mua có dự phòng (`scm-requirement`), nhiệt độ tự do (`qc-temperature`), hao hụt (`prod-waste`), bảng đơn (`crm-order`), danh sách thực đơn (`menu-menu`), danh sách phản ánh (`log-complaint`), chi tiết / hủy yêu cầu duyệt (`wf-inbox`), lô + truy xuất 2 chiều (`scm-lot`, `prod-batch`). **Có thay đổi hành vi P7 / P8** (xếp xe trước khi xuất phát) – xem đầu file | FE-ALL | BE2.1 | `be2.1-flow-gaps.http` |

**Luồng Pilot đầy đủ P3 → P8** (thứ tự gọi API, dữ liệu mẫu, các chốt an toàn): `docs/api-tests/pilot-flow.http` + `docs/claude/UAT_CHECKLIST.md` mục PILOT.

Lưu ý chung cho FE (T50R1):
- Danh sách `GetAll` không truyền Id cha (vd. `MaterialLot/GetAll` không có `IngredientId`) nay trả **đủ dòng**; truyền Guid rỗng cũng coi như không lọc.
- CRUD `Insert` của danh mục trả `data = true` (không trả Id) → sau khi thêm, tải lại danh sách (`GetAll?SearchText=<mã>`).
- Số chứng từ do server sinh (`PH-`, `PO-`, `XN-`, `KH-`, `NL-`, `ME-`, `CX-`, `PA-`), không nhập tay.
