Kiểm tra chất lượng toàn bộ trước khi bàn giao:
1. dotnet build AppDelicare/AppDelicare.sln và dotnet test – báo lỗi, cảnh báo mới.
2. Xác nhận KHÔNG có thay đổi nào trong app-delicare-client/ (git status / git diff --stat). Có thì báo ngay – frontend ngoài phạm vi.
3. Quét vi phạm luật cứng (CLAUDE.md mục 3): grep các lệnh CREATE TABLE / ALTER TABLE / ADD / FOREIGN KEY / CREATE SCHEMA trong DB/ và code; tên bảng/cột trong code không có trong docs/reference/20261113_structure_db_appdelicare.sql; chuỗi giống bí mật (key, token, password) trong file được git theo dõi; SQL ghép chuỗi.
4. Đối chiếu task DONE trong ROADMAP với AC – liệt kê AC chưa có bằng chứng.
Báo cáo dạng bảng: mục kiểm tra · kết quả · file/ dòng · đề xuất sửa. Chỉ sửa khi anh đồng ý. $ARGUMENTS
