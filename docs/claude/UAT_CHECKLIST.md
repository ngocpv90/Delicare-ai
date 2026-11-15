# UAT_CHECKLIST – kịch bản thử bằng API

Thử trên **staging** (deploy/README.md). Công cụ: Swagger (`/swagger`) hoặc VS Code REST Client (`docs/api-tests/*.http`).
Đánh dấu [x] khi đạt; ghi lỗi / góp ý vào cột "Ghi chú" hoặc báo Claude Code.

## R1.0 – Nền móng (2026-11-14) · file thử: `docs/api-tests/r1.0-foundation.http`, `docs/api-tests/v1-hr-shifts.http`

| # | Kịch bản | Cách thử | Kỳ vọng | Đạt | Ghi chú |
|---|---|---|---|---|---|
| 1 | Sức khỏe hệ thống | `GET /health` | `Healthy`, database `Healthy`, đúng environment | [ ] | |
| 2 | Swagger | mở `/swagger` | Có nhóm `v1 / hr / shifts`, `v1 / scm / inventory-transactions`, `v1 / audit`, `legacy / …` | [ ] | |
| 3 | Đăng nhập đúng / sai | `POST /api/Authentication/Login` | Đúng → token; sai mật khẩu → 400 "Tài khoản hoặc mật khẩu không đúng" | [ ] | |
| 4 | Chặn khi chưa đăng nhập | gọi API bất kỳ không token | 401 (v1: envelope `UNAUTHORIZED`) | [ ] | |
| 5 | Cơ cấu tổ chức | `GET /api/OrganizationUnit/GetAll` | 33 đơn vị đúng sơ đồ (DLC → CEO → 5 khối + Ban cố vấn; OPS-KIT có 6 tổ) | [ ] | Anh soát tên / mã |
| 6 | Nhóm quyền | `GET /api/UserGroup/GetAll` | 16 nhóm: CEO, CEO-PMO, COO, OPS-PLAN, OPS-SCM, OPS-KIT, OPS-LOG, OPS-POS, OPS-MNT, SAL-SALES, SAL-CON, SAL-CS, FIN-ACC, FIN-HR, FIN-IT, QA | [ ] | |
| 7 | Ma trận quyền | `GET /api/UserRole/GetByUserGroupId` cho từng nhóm | Đúng ma trận ORG_STRUCTURE mục 2 (PROPOSED, D-25) | [ ] | **Anh duyệt** |
| 8 | Phân quyền thật | tạo user, gán vào 1 nhóm (vd. OPS-SCM), đăng nhập user đó | Xem được màn được phép; thêm / sửa màn không được phép → 403 | [ ] | |
| 9 | Duyệt | (khi có API duyệt) user OPS-SCM duyệt kế hoạch sản xuất | Bị từ chối; COO / CEO được | [ ] | Nhóm duyệt: appsettings `Permissions:ApproverGroups` |
| 10 | Ca làm việc v1 | `docs/api-tests/v1-hr-shifts.http` | Tạo ca qua đêm; trùng mã 409; giờ bắt đầu = kết thúc 422; ETag cũ 409; gửi 2 lần cùng Idempotency-Key chỉ tạo 1 | [ ] | |
| 11 | Xóa bản ghi đang dùng | xóa ca đang có phân ca / đơn hàng tham chiếu | 409 `IN_USE`, nêu bảng đang dùng | [ ] | |
| 12 | Sổ kho chỉ ghi thêm | gọi `Update` / `Delete` sổ kho; đảo bút toán 2 lần | Không có Update / Delete; đảo lần 2 → 409 | [ ] | |
| 13 | Lịch sử xử lý | `GET /api/v1/audit/{table}/{id}/history` sau khi sửa 1 bản ghi | Có dòng: thời điểm, người thao tác, nội dung thay đổi | [ ] | |
| 14 | Bảo mật | xem response đăng nhập / danh sách user | Không bao giờ thấy trường mật khẩu | [ ] | |
