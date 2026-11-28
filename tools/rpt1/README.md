# tools/rpt1 – nạp lịch sử Kết quả kinh doanh (RPT-1.3, D-140 i)

Công cụ **chạy một lần** để có ngay xu hướng 12 tháng: đọc các sheet ẩn của file báo cáo kế toán (`Report_YYYYMM_sent.xlsx` – **không đưa vào repo**, số liệu tài chính thật) và nạp vào lớp **Chính thức (AMIS)** qua API `Dashboard/ImportAmisLines` – cùng quy tắc cộng với nút *Nhập sổ AMIS* (RPT-1.2).

```bash
# chỉ so với sheet Data_DB, không ghi
dotnet run --project tools/rpt1 -- "D:\...\Report_202608_sent.xlsx" --check
# nạp thật (tài khoản có quyền Nhập mục Kết quả kinh doanh – Kế toán)
RPT1_USER=<tài khoản> RPT1_PASS=<mật khẩu> dotnet run --project tools/rpt1 -- "D:\...\Report_202608_sent.xlsx" [--api http://localhost:5125/api] [--months 202601-202608]
```

1. **Quy tắc phân loại** sinh từ chính phân loại của Kế toán trong file (GVHB: 621 → *Phân loại_NVL*, 627 → *Phân loại*; CP: 641 / 642 / 811 → *Phân loại*) – mỗi diễn giải một quy tắc, đặt trước quy tắc đang có (nhóm NVL "Bữa sáng" đổi tên "Đồ ăn sáng mua sẵn" – D-140 f).
2. **Mỗi tháng**: sổ 511 (DTBH), 621 / 622 / 627 (GVHB), 641 / 642 / 811 (CP), 711 (TNK – lấy số bút toán kết chuyển 911 vì sheet thiếu dòng phát sinh ở vài tháng), bảng cân đối mã 280 / 400 (Raw BCD) → nạp; nạp lại = ghi đè (không nhân đôi).
3. **So** từng tháng với sheet Data_DB (Doanh thu, GVHB, Lợi nhuận gộp, Chi phí, Lợi nhuận ròng – triệu đồng): in KHỚP / LỆCH, mã thoát 0 khi khớp hết.

Kết quả trên DB dev 28/11/2026 (file `Report_202608_sent.xlsx`): 8 tháng × 5 chỉ tiêu **khớp**; quý 3 lũy kế doanh thu 25.072,04 trđ.
