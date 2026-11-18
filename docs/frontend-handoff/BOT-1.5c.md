# BOT-1.5c – Bot Vận hành (phần còn lại) – API mới cho dev FE

| API | Quyền | Vào / ra | Ghi chú |
|---|---|---|---|
| `GET api/ShiftAssignment/GetSwapCandidates?assignmentId=` | Chỉ cần đăng nhập (ca của chính mình, còn mở) | `data = { current { assignmentId, workDate, shiftName, plannedStart, plannedEnd, assignmentStatus }, listShift[] { employeeId, employeeName, assignmentId, workDate, shiftName, plannedStart, plannedEnd }, listColleague[] { employeeId, employeeName } }` | Ca còn mở của đồng nghiệp cùng đơn vị ± 7 ngày (từ hôm nay), tối đa 30. Chỉ tên + ca. Dùng được cho màn "Đổi ca" trên web / điện thoại trước khi gọi `ShiftSwapRequest/Create`. |

Không đổi hành vi API cũ. Bot dùng lại API có sẵn bằng JWT của chính người dùng (phản ánh, chấm hộ, tồn kho, MRP, PO, phiếu nhập, kế hoạch, đơn hàng, hao hụt, giá thành, hộp khay, chuông cảnh báo, job) – ghi qua bot hiện y như web (lý do / nguồn có chữ "Telegram", chấm hộ hình thức PROXY).

Không có bảng / cột mới – sau `git pull` chỉ cần build lại API.
