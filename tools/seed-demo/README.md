# Seeder dữ liệu thử [DEMO] – cho máy dev FE và server test

Tạo một bộ dữ liệu thử **đầy đủ toàn luồng R1** bằng cách gọi API thật, theo đúng thứ tự trong [`docs/api-tests/full-flow.http`](../../docs/api-tests/full-flow.http) (217 request):

danh mục → hợp đồng → dự báo → thực đơn → đơn hàng → mua hàng (duyệt 2 cấp, PO Giám đốc duyệt) → nhận hàng + QC → kho → kế hoạch → sản xuất theo tổ / ca → đóng gói → chuyến xe → bàn giao + POD → thu hồi hộp, vệ sinh → đối soát → giá thành → in / xuất → nhân sự (phân ca, chấm công).

- **Dữ liệu đúng nghiệp vụ**: số chứng từ, trạng thái duyệt, tồn kho, giá thành… do API tự tính. Không chèn SQL trực tiếp.
- **Không xóa / sửa dữ liệu có sẵn**. Mỗi lần chạy tạo **1 bộ mới**: mã danh mục `DEMO<key>` (tự sinh) và **ngày ăn chưa có kế hoạch sản xuất** trong 20 ngày tới (tự tìm).
- Mọi tên đều có tiền tố `[DEMO]`, dễ lọc / nhận ra.

## Cần có

- Node.js 18 trở lên (dùng `fetch` sẵn có, không cần `npm install`).
- API đang chạy và đã cập nhật DB (`deploy/db/Deploy-Database.ps1` – SP + seed danh mục chuẩn).
- Tài khoản **admin** của môi trường đó.

## Chạy

```powershell
# Máy dev (API dotnet run – http://localhost:5125)
node tools/seed-demo/seed-demo.mjs --base http://localhost:5125 --password admin

# Server test – mật khẩu đặt ở biến môi trường, không gõ lên dòng lệnh / không commit
$env:DELICARE_ADMIN_PASSWORD = "<mật khẩu admin server test>"
node tools/seed-demo/seed-demo.mjs --base https://<địa chỉ server test>

# Nhiều bộ dữ liệu (mỗi bộ một ngày ăn khác nhau)
node tools/seed-demo/seed-demo.mjs --base http://localhost:5125 --password admin --rounds 3
```

Tùy chọn: `--user` (mặc định `admin`), `--key` (mã bộ dữ liệu, vd. `F01`), `--day` (ngày ăn `YYYY-MM-DD`, phải chưa có kế hoạch sản xuất), `--file` (file `.http` khác), `--out` (thư mục kết quả).

Kết quả: in `217/217 đạt` (hoặc liệt kê bước lỗi) và lưu chi tiết từng request vào `tools/seed-demo/out/` (thư mục không commit). Mã thoát 0 = tất cả đạt.

## Sau khi chạy – tìm dữ liệu trên giao diện

Tìm theo mã `DEMO<key>` in ra ở dòng `=== Lượt …` (vd. `DEMOST8R1`): khách hàng, món, nguyên liệu, nhà cung cấp, kho, nhân viên… và **ngày ăn** in cùng dòng (đơn hàng, kế hoạch, mẻ, chuyến xe của ngày đó).

## Lưu ý

- Giờ "hôm nay" tính theo **giờ Việt Nam**, không phụ thuộc múi giờ máy chạy. Máy chạy **SQL Server** vẫn nên đặt múi giờ Asia/Ho_Chi_Minh (`/health` báo `dbclock` lệch).
- Có bước lỗi: các bước trước đó **đã tạo dữ liệu** (không tự hoàn tác). Sửa nguyên nhân rồi chạy lại – lượt mới dùng mã và ngày mới.
- Không dùng trên **production**.
