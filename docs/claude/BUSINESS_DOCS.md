# BUSINESS_DOCS – Tài liệu nghiệp vụ thật của Delicare (nằm NGOÀI repo)

Thư mục: `D:\Duan\Tai lieu nghiep vu du an\Delicare-Share`. Đây là file Excel vận hành thật do anh cung cấp ngày 2026-11-14.

**Luật dùng:**
- Chỉ **đọc**. Không sửa, không chép file hoặc dữ liệu vào repo.
- File có tên NCC, số điện thoại, giá mua, số liệu tài chính → **không commit**, không đưa vào log hay seed. Chỉ lấy **cấu trúc** (cột, công thức, bố cục biểu mẫu) và **quy tắc**.
- Seed demo phải tự tạo, gắn nhãn `[DEMO]`.
- Dữ liệu thật chỉ vào DB qua chức năng Import (T49) do người dùng thực hiện.

**Không có tài liệu BRD gốc** (anh xác nhận). Nguồn nghiệp vụ gồm: các file dưới đây, Dictionary/BRD Excel trong `docs/reference/`, mockup, và BUSINESS_RULES.md.

## Thông tin pháp nhân (dùng cho header biểu mẫu in, trang đăng nhập)
- Tên: **CÔNG TY CỔ PHẦN THƯƠNG MẠI DỊCH VỤ DELICARE VN**
- Địa chỉ: Lô E2A, Cụm Công nghiệp Thực phẩm Hapro, Xã Thuận An, TP Hà Nội
- Điện thoại: 0934 660 640 · Website: Delicare.vn
- Màu thương hiệu: **#91AF42** (xanh lá) / **#F07825** (cam). Logo: **chưa có file** → dùng chữ "DELICARE" tạm, để chỗ thay logo cấu hình được (`assets/brand/`).

Lưu thông tin trên ở cấu hình `Company` (bảng `Company` có sẵn) hoặc appsettings `Branding`, không hard-code trong template.

## Danh sách file và cách dùng

| File | Nội dung | Dùng cho |
|---|---|---|
| `Menu/Rule Menu.xlsx` – sheet **Rule** | Quy tắc lên thực đơn: nguyên liệu chính, tần suất, **định lượng món chính 1 / 2 (gram)**, mẫu phối món theo thứ (món chính 1, món chính 2, món phụ, rau, canh) | Quy tắc thực đơn (P3); kiểm tra lặp món; định lượng mặc định của RecipeLine |
| `Menu/Rule Menu.xlsx` – sheet **Ngân hàng món** (~367 dòng) | Danh mục món theo nhóm: món ăn sáng, món chính, món phụ, cơm, rau, canh, tráng miệng, món ăn đêm | Seed / Import `DishCategorie` + `Dishe` (T49) |
| `Menu/Rule Menu.xlsx` – sheet **Chi tiết món và hao hụt, giá** (~4.600 dòng) | Mỗi món: thành phần nguyên liệu, **mã NL** (AD0001, AS0002…), ĐVT, giá, **% hao hụt sơ chế**, **% hao hụt theo phương pháp chế biến** (rang, xào, ninh, chiên, rán, hầm, kho, rim, sốt, bỏ lò, luộc, hấp, canh, quay, nướng) | Import `Ingredient`, `Recipe`, `RecipeLine` (`WastePercent` = hao hụt sơ chế, `ExpectedWasteRate` = hao hụt chế biến); công thức MRP (T14) |
| `Menu/New_Menu_Delicare_202611_W3.xlsx` | Thực đơn tuần thật: sheet theo thứ; mỗi **buổi** SÁNG / TRƯA / TỐI / ĐÊM có **Lựa chọn 1 / 2 / 3**, số suất dự kiến / thực tế, giá vốn, giá bán, **FC (food cost %)**, lợi nhuận; sheet "4. Menu_Sent" là **mẫu thực đơn gửi khách**; Sheet1 là bảng % hao hụt theo phương pháp | Màn hình thực đơn tuần (P3), tính giá vốn / FC; **mẫu in "Thực đơn gửi khách"** |
| `Menu/Phieu_Truoc sx.xlsx` | **PHIẾU TRƯỚC SẢN XUẤT** theo thứ: buổi, nhóm, món, nguyên liệu, ĐVT, định lượng trước sơ chế, tổng theo order kế hoạch. Sheet Raw: Month, Week, Thu, Buoi, Phan_loai, Mon, Nguyen_lieu, DVT, DL_truoc_so_che, Ty_le_hao_hut_so_che… | **Mẫu in** + báo cáo nhu cầu nguyên liệu trước sản xuất (P4/P5, sau MRP) |
| `Menu/Phieu san xuat.xlsx` | **PHIẾU SẢN XUẤT** theo thứ: số suất order / thực tế, món, nguyên liệu, định lượng | **Mẫu in** phiếu sản xuất / lệnh sản xuất (P5) |
| `Menu/Tool_Tong hop so suat.xlsm` | Tổng hợp **số suất theo ngày × khách × địa điểm × vị trí giao nhận** (ví dụ: Vincons – Cổ Loa – A5) và theo buổi / lựa chọn | Màn hình nhập / tổng hợp đơn theo điểm giao (P3); `Customer` → `CustomerDeliveryLocation` (Key_map) |
| `Purchase/Tool_Tong hop bao gia_1011.xlsm` | Quy trình: Kế hoạch order (từ menu) → tổng hợp báo giá từ nhiều NCC → **so sánh giá** → **Form PO (ĐƠN ĐẶT HÀNG)**; danh sách NCC; bảng giá (mã NL, ĐVT chính, đơn giá chưa VAT, % VAT) | P4: SupplierMaterial (giá, VAT, NCC ưu tiên), so sánh báo giá, **mẫu in Đơn đặt hàng NCC** |
| `DB/Report_202608_sent.xlsx` (**MẬT – tài chính**) | Báo cáo kết quả hoạt động tháng: Dashboard, Monthly, YTD (doanh thu, giá vốn, lợi nhuận gộp / thuần, chi phí), dữ liệu kế toán DTKH / GVHB / CP | **Chỉ tham khảo bố cục** dashboard điều hành và KPI tài chính (P8). **Không đọc số liệu vào code, test hay tài liệu** |
| `DB/202608/*` (**MẬT – sổ kế toán, ngân hàng**) | Sổ chi tiết TK 511 / 621 / 622 / 627 / 641 / 642…, BCTC, tồn kho NVL / TP | Không dùng trong R1. Không mở trừ khi anh yêu cầu |

## Quy tắc rút ra (đã chép vào BUSINESS_RULES.md)
- Buổi ăn: **SÁNG, TRƯA, TỐI, ĐÊM**. Mỗi buổi có thể có **nhiều lựa chọn món (Lựa chọn 1–3)** với số suất riêng.
- Giá vốn và FC% tính theo buổi / lựa chọn; giá bán theo hợp đồng (ví dụ: sáng 20.000đ, trưa 40.000đ chưa VAT – chỉ là ví dụ trong file, không hard-code).
- Hao hụt hai tầng: **sơ chế** (theo nguyên liệu) và **chế biến** (theo phương pháp nấu).
- Mã nguyên liệu dạng `AS0001`, `AD0001`… (tiền tố theo nhóm). Giữ nguyên mã khi import.
- Đơn theo **khách → địa điểm → vị trí giao nhận**.
