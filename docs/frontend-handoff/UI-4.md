# UI-4 – Màn chi tiết nhân viên (D-68) · 2026-11-19

Màn FE đã làm: `adm-employee-360` (`modules/employee360.js`, khung chung `modules/detail360.js`). Tài liệu này ghi API để soát / dùng lại.

| API | Thay đổi | Ghi chú |
|---|---|---|
| `GET api/Employee/GetOverview?id=` | **Mới** | `employee` (ngày sinh / giới tính / địa chỉ = null khi người xem không có quyền **Sửa** adm-employee – `canViewPersonal`), `organizationUnitName`, `positionName`, `tenureMonths`, `userId` / `userName` / `userIsActive` / `listUserGroupName`, `botLinked` / `botStatus` / `botUsername` / `botLinkedAt`, `monthShifts` / `monthWorkedHours` / `monthOnTimePct` / `monthAbsent` / `monthLate`, `yearLeaveDays` + `listLeaveByType`, `pendingRequestCount`, `listUpcomingShift` (hôm nay → +7, dạng TimesheetDetail), `listLeaveDate`, `listDayHours` (30 ngày), `listRecentAttendance` (10 ca), `listAlert` (`NOT_CHECKED_IN`, `OUT_OF_RADIUS`, `PENDING_REQUEST`, `NO_BOT_LINK`, `NO_PHONE`) |
| `GET api/ShiftAssignment/GetAll` | Thêm `FromDate`, `ToDate` | Ngày làm, gồm cả 2 đầu (SP `uspShiftAssignment_GetAll`) |

## Định nghĩa
- Tài khoản web: liên kết bot có UserId; không có thì tên đăng nhập = mã NV (giống `HrRules.ResolveEmployeeAsync`).
- Chỉ số tháng / ca / giờ công lấy từ `AttendanceEventService.GetTimesheetDetail` (cùng quy tắc muộn, về sớm, vắng với Bảng công). Đúng giờ = ca đã vào ca không muộn ÷ ca đã vào ca.
- Ngày nghỉ năm: đơn **Đã duyệt**, cắt theo năm hiện tại. Đơn chờ: nghỉ (của nhân viên) + đổi ca (người đề nghị hoặc người nhận).
- Ngoài bán kính: AttendanceEvent `IsValid = false` trong 7 ngày (≥ 2 lần → cảnh báo).

Quyền: xem `adm-employee` (màn `adm-employee-360` dùng quyền màn cha – `PERM_ALIAS`). Test: `EmployeeOverviewTests` (kể cả người chỉ có quyền Xem không nhận thông tin cá nhân), `SearchFilterSqlServerTests.Shift_assignment_list_filters_by_work_date_range`; `full-flow.http` 17.5 – 17.6.
