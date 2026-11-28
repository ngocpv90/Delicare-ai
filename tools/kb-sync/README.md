# kb-sync – Obsidian máy cá nhân (AI-18, D-133)

Đồng bộ kho tri thức DELI-CARE xuống một thư mục để mở bằng Obsidian. Node 18+, không thư viện ngoài. Chi tiết cho người dùng: `docs/user-guide/16_KHO_TRI_THUC.md` mục AI-18.

```bash
DC_KB_URL=https://<máy chủ>/api DC_KB_USER=<tài khoản> DC_KB_PASS=<mật khẩu> node tools/kb-sync/kb-sync.mjs "<thư mục vault>" [--dry]
```

- Máy chủ: `Knowledge/GetSyncManifest` (đường dẫn + băm SHA-256 các ghi chú người dùng được xem, KHÔNG BOD) → tải tệp đổi qua `Knowledge/GetNote`; đẩy lên qua `Knowledge/Save` (prevHash).
- Một chiều mọi thư mục; hai chiều chỉ `80 Đào tạo` khi `Knowledge:SyncTwoWayTraining = true` và có quyền Sửa thư mục 80.
- Trạng thái: `<vault>/.delicare-sync.json`; bản máy bị thay / xung đột: `<vault>/.delicare-conflict/`.
- Lịch sử: AuditChange `KnowledgeSync` mỗi lần lấy danh sách.
