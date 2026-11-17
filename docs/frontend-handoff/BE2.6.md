# Bàn giao frontend – BE2.6: nhân sự (T21)

Quy ước chung (đăng nhập, `Result` / `Results`, lỗi nghiệp vụ = HTTP 200 + `responseCode 400` + `responseMessage` tiếng Việt, quyền theo `listMenu`): `P0-P2.md` mục 1.
Form: số `10.8231`, ngày `yyyy-MM-dd`, ngày giờ `yyyy-MM-ddTHH:mm:ss`, danh sách lặp khóa (`EmployeeIds=<id>&EmployeeIds=<id>`, `SkipWeekdays=6&SkipWeekdays=7`).
Mọi action ghi nhận **form**, đọc = GET query. Thử nhanh cả luồng (1 bộ `[DEMO]` tự đủ, đã chạy trên DB dev): `docs/api-tests/be2.6-hr.http`.

Màn hình (đã có trong seed menu / quyền T07 – **không seed mới**): `#/hr-shift-assign` (Lịch phân ca), `#/hr-attendance` (Chấm công theo ca), `#/hr-leave` (Đơn xin nghỉ),
`#/hr-swap` (Đơn đổi ca); hộp duyệt `#/wf-inbox?type=hr-leave` ("Duyệt – Đơn xin nghỉ"), `#/wf-inbox?type=hr-swap` ("Duyệt – Đơn đổi ca") – seed cho CEO / COO / FIN-HR.
Danh mục: `GET MetaData/GetReferenceLists?name=LeaveType` (loại nghỉ), `name=AttendanceSource` (hình thức chấm công); trạng thái: `MetaData/GetStatuses` (`ShiftAssignment.AssignmentStatus`,
`LeaveRequest.RequestStatus`, `ShiftSwapRequest.RequestStatus`, `AttendanceEvent.EventType`).

## Quyền
| Action | Quyền |
|---|---|
| `ShiftAssignment/GetGrid` | Xem `#/hr-shift-assign` |
| `ShiftAssignment/Assign`, `BulkAssign`, `Confirm`, `MarkAbsent` | Sửa `#/hr-shift-assign` |
| `AttendanceEvent/GetTimesheet`, `GetTimesheetDetail` | Xem `#/hr-attendance` |
| `AttendanceEvent/CheckIn`, `CheckOut`, `GetMine` | **Mọi tài khoản đã đăng nhập** (tự phục vụ). Chấm cho nhân viên khác (`EmployeeId` khác mình – máy chấm công, chấm hộ, bot) cần **Thêm** `#/hr-attendance` |
| `LeaveRequest/Create`, `ShiftSwapRequest/Create`, `GetMine`, `GetDetail` | Mọi tài khoản (cho chính nhân viên của mình). Lập đơn cho người khác cần **Thêm** `#/hr-leave` / `#/hr-swap`. Xem chi tiết: người tạo, nhân viên của đơn, quyền Xem màn, quyền Duyệt |
| `LeaveRequest/Approve`, `Reject` · `ShiftSwapRequest/Approve`, `Reject` | Duyệt (`AllowEdit`) trên `#/wf-inbox?type=hr-leave` / `hr-swap` – kiểm trong engine (như `ApprovalRequest/Decide`) |
| `LeaveRequest/Cancel`, `ShiftSwapRequest/Cancel` | Người tạo đơn (admin được) – engine kiểm |

**Tài khoản ↔ nhân viên (GAP-HR-02):** không có cột nối User ↔ Employee. Nhân viên của tài khoản = (1) `UserExternalIdentity` có UserId + EmployeeId, (2) **mã nhân viên (`Employee.Code`) = tên đăng nhập**,
(3) SĐT hồ sơ (Person) = SĐT nhân viên (khớp đúng 1). Không tìm được → 400 "Tài khoản chưa gắn với nhân viên…". FE màn chấm công nên gọi `AttendanceEvent/GetMine` trước.

## ⚠ Thay đổi hành vi
1. CRUD `ShiftAssignment/Insert`, `Update`, `Delete` → **400** (chỉ qua `Assign` / `BulkAssign`); `AttendanceEvent/Insert` → 400 (chỉ `CheckIn` / `CheckOut`); `LeaveRequest` / `ShiftSwapRequest` `Insert` / `Update` → 400 (chỉ `Create`), `Delete` chỉ đơn Từ chối / Hủy. `GetAll` / `GetById` / `GetAllData` giữ nguyên.
2. Mỗi nhân viên **1 ca / ngày** trên lưới (spec "upsert theo (EmployeeId, WorkDate)") – PROPOSED D-55 (d).
3. Bỏ ca = xóa mềm; phân lại đúng ca / ngày đó dùng lại dòng cũ (UNIQUE `EmployeeId, ShiftId, WorkDate` tính cả dòng đã xóa) – FE không cần xử lý gì, `assignmentId` có thể là Id cũ.

## 1. Lưới ca `#/hr-shift-assign` (Catalog 201–204)

| Chức năng | Action | Input | Kết quả |
|---|---|---|---|
| Lưới nhân viên × ngày | `GET ShiftAssignment/GetGrid` | `FromDate?` (trống = hôm nay), `Days?` (1–31, mặc định 7), `OrganizationUnitId?` (gồm đơn vị con), `SearchText?` | `{ fromDate, toDate, days[], shifts[], employees[{ …, cells{ "yyyy-MM-dd": ô } }], kpiToday[] }` |
| Phân / đổi / bỏ ca | `PUT ShiftAssignment/Assign` | `EmployeeId, WorkDate, ShiftId?` (trống = **bỏ ca**), `PlannedStart?, PlannedEnd?` (trống = giờ ca; ca qua đêm kết thúc hôm sau), `Note?` | `{ action: Created / Updated / Removed / Unchanged, assignment, listWarning }` |
| Phân ca hàng loạt | `POST ShiftAssignment/BulkAssign` | `EmployeeIds` (lặp; trống = mọi nhân viên của `OrganizationUnitId`), `ShiftId, FromDate, ToDate` (≤ 31 ngày), `SkipWeekdays` (lặp, 1 = Thứ 2 … 7 = CN), `Note?` | `{ created, skipped, listSkipped[{ employeeId, employeeName, workDate, reason }] }` – không ghi đè ô đã có ca / ngày nghỉ phép |
| Xác nhận lịch | `POST ShiftAssignment/Confirm` | `WorkDate, ToDate?, OrganizationUnitId?, ShiftId?` | `{ fromDate, toDate, confirmed, alreadyConfirmed, notificationSent: false }` – **không gửi Telegram** (tích hợp bot để sau) |
| Đánh vắng (thay job) | `POST ShiftAssignment/MarkAbsent` | `WorkDate, OrganizationUnitId?` | `{ marked, listAssignment[] }` – ca Kế hoạch / Xác nhận đã hết giờ mà không vào ca → Vắng |

Chặn (400): ngày đã qua; nhân viên nghỉ việc (`Status` Inactive / Resigned); ca ngừng dùng; **có đơn nghỉ Đã duyệt** bao ngày đó; ca của ngày **đã chấm công / đã vào ca / Hoàn thành / Vắng** ("dùng đơn đổi ca"); ca đang trong **đơn đổi ca chờ duyệt**.
Đổi giờ ca đã Xác nhận → về Kế hoạch + `listWarning` "cần xác nhận lại". Có đơn nghỉ Chờ duyệt ngày đó → vẫn phân + `listWarning`.

Ô lưới:
```json
"cells": {
  "2027-01-11": { "assignmentId": "fa41c8fe-…", "shiftId": "07c9690f-…", "shiftCode": "TRUA", "shiftName": "Trưa", "plannedStart": "2027-01-11T10:30:00", "plannedEnd": "2027-01-11T13:30:00",
    "status": "Confirmed", "statusName": "Xác nhận", "onLeave": false, "leavePending": false, "leaveRequestId": null, "leaveRequestNo": null, "hasAttendance": false, "pendingSwap": false },
  "2027-01-12": { "assignmentId": null, "shiftCode": null, "status": null, "onLeave": true, "leavePending": false, "leaveRequestId": "b3ec25ac-…", "leaveRequestNo": "DN-261117-0004", "hasAttendance": false, "pendingSwap": false }
},
"kpiToday": [ { "shiftId": "…", "shiftCode": "TRUA", "shiftName": "Trưa", "assigned": 12, "checkedIn": 10 } ]
```
Màu ô theo mockup: mã ca (`scode <shiftCode>`), `onLeave` = "Nghỉ", `status = Absent` = "Vắng". Bấm ô → modal chọn ca (gọi `Assign`), nút "Bỏ phân ca" (`ShiftId` trống).
Trạng thái ca: `Planned` Kế hoạch → `Confirmed` Xác nhận → `CheckedIn` Đã vào ca → `Completed` Hoàn thành / `Absent` Vắng.

## 2. Chấm công `#/hr-attendance` (Catalog 205, 206)

| Chức năng | Action | Input |
|---|---|---|
| Vào ca | `POST AttendanceEvent/CheckIn` | `EmployeeId?` (trống = chính mình), `Latitude?, Longitude?, SourceChannel?` (`GPS` mặc định / `QR` / `MACHINE` / `PROXY`; nhận cả `MOBILE`, `ZALO` → GPS, `KIOSK` → MACHINE), `WorkstationCode?` (QR: bắt buộc), `ExternalEventId?` (chống trùng), `Note?` (PROXY: bắt buộc lý do) |
| Ra ca | `POST AttendanceEvent/CheckOut` | như trên |
| Ca hôm nay của tôi | `GET AttendanceEvent/GetMine` | – → `{ employeeId, employeeName, listAssignment[] (như chi tiết bảng công), allowedActions: ["checkIn"] / ["checkOut"] }` |
| Bảng công (theo nhân viên) | `GET AttendanceEvent/GetTimesheet` | `FromDate, ToDate` (≤ 62 ngày), `OrganizationUnitId?, EmployeeId?, SearchText?, Page, PageSize` |
| Bảng công chi tiết (theo ca) | `GET AttendanceEvent/GetTimesheetDetail` | như trên |

Quy tắc (PROPOSED D-55 c, e):
- Ca được tìm tự động: vào ca = ca Kế hoạch / Xác nhận hôm nay (hoặc ca đêm hôm qua) khi giờ hiện tại trong [bắt đầu − 120′, kết thúc]; ra ca = ca Đã vào ca. Không có → 400 (kèm giờ vào ca được sớm nhất).
- **GPS:** `distanceMeters` = khoảng cách tới trạm bếp (`KitchenWorkstation` Hoạt động có tọa độ) gần nhất; `isValid` = khoảng cách ≤ `RadiusMeters` (trống → 150 m). **Ngoài bán kính vẫn ghi**, `isValid = false` + `listWarning` "Ngoài bán kính bếp: cách trạm … m (cho phép … m)" (HR xem trên bảng công; bật `BusinessRules:Hr:RejectOutsideRadius` để chặn). Thiếu tọa độ → 400.
- **QR:** `WorkstationCode` = mã trạm in trên QR. **MACHINE / PROXY** chỉ khi chấm cho người khác (cần quyền); Note tự ghi "Ghi bởi <tài khoản> (<hình thức>)".
- Đi muộn / về sớm so với giờ kế hoạch (ân hạn 5′) → `lateMinutes` / `earlyLeaveMinutes` + Note. Gửi lại cùng `ExternalEventId` → `isDuplicate = true`, không ghi thêm.
- Kết quả **không trả lại tọa độ** (dữ liệu cá nhân); FE không lưu / không log tọa độ.

```json
{ "eventId": "…", "employeeId": "…", "employeeName": "[DEMO] Lê Văn Cường", "eventType": "CHECK_IN", "eventTypeName": "Vào ca", "eventAt": "2026-11-17T11:07:25",
  "sourceChannel": "GPS", "sourceChannelName": "GPS (điện thoại)", "isValid": true, "distanceMeters": 22.24, "radiusMeters": 100, "workstationCode": "DEMOB26A-KW",
  "workstationName": "[DEMO] Cổng bếp", "assignment": { "id": "…", "shiftCode": "TRUA", "workDate": "2026-11-17T00:00:00", "status": "CheckedIn", "statusName": "Đã vào ca" },
  "lateMinutes": 10, "earlyLeaveMinutes": 0, "isDuplicate": false, "note": "Trạm DEMOB26A-KW; Đi muộn 10 phút; Ghi bởi admin (GPS (điện thoại))", "listWarning": [] }
```
Bảng công (1 dòng / nhân viên):
```json
{ "stt": 1, "employeeId": "…", "employeeCode": "DEMOB26B-NV3", "fullName": "[DEMO] Lê Văn Cường", "organizationUnitName": null, "positionName": null,
  "assignedShifts": 1, "present": 1, "late": 1, "lateMinutes": 10, "earlyLeave": 1, "earlyLeaveMinutes": 119, "absent": 0, "missingCheckOut": 0,
  "invalidEvents": 1, "leaveDays": 0, "workedMinutes": 0, "workedHours": 0 }
```
`absent` = ca Vắng **hoặc** ca đã hết giờ mà chưa vào ca (không cần chạy `MarkAbsent`); `missingCheckOut` = đã vào ca, hết giờ chưa ra ca; `invalidEvents` = lần chấm ngoài bán kính.
Chi tiết (1 dòng / ca): `checkInAt, checkOutAt, checkInSource, checkOutSource, checkInValid, checkOutValid, checkInDistanceMeters, checkOutDistanceMeters, lateMinutes, earlyLeaveMinutes, workedMinutes, isAbsent, missingCheckOut, status, statusName`.

## 3. Đơn xin nghỉ `#/hr-leave` (Catalog 207, 209)

| Chức năng | Action | Input |
|---|---|---|
| Lập đơn + gửi duyệt | `POST LeaveRequest/Create` | `EmployeeId?` (trống = chính mình), `FromDate, ToDate` (≤ 30 ngày, lùi tối đa 7 ngày), `LeaveTypeCode` (`ANNUAL` / `SICK` / `PERSONAL` / `UNPAID` / `MATERNITY` / `OTHER`), `Reason` (bắt buộc) |
| Chi tiết | `GET LeaveRequest/GetDetail?id=` | |
| Đơn của tôi | `GET LeaveRequest/GetMine` | (≤ 100, mới nhất trước) |
| Danh sách (màn HR) | `GET LeaveRequest/GetAll?EmployeeId=&RequestStatus=&Page=&PageSize=` | SP cũ |
| Duyệt / từ chối | `POST LeaveRequest/Approve` · `Reject` | `Id, Note` (từ chối: bắt buộc) – hoặc `ApprovalRequest/Decide` từ hộp duyệt |
| Hủy | `POST LeaveRequest/Cancel` | `Id, Reason` – chỉ đơn Chờ duyệt, người tạo |

Số đơn `DN-YYMMDD-NNNN`. Chặn: trùng khoảng ngày với đơn Chờ duyệt / Đã duyệt khác; ngày đã chấm công. Duyệt (1 cấp mặc định; nhiều cấp bằng `BusinessRules:ApprovalSteps:LEAVE_REQUEST`)
→ **Đã duyệt + bỏ các ca Kế hoạch / Xác nhận chưa chấm công trong khoảng nghỉ** → chi tiết có `listWarning` "Ca … ngày … thiếu người: … nghỉ (đã bỏ phân ca) – cần phân người thay".
```json
{ "request": { "id": "…", "requestNo": "DN-261117-0004", "employeeId": "…", "fromDate": "2027-01-12T00:00:00", "toDate": "2027-01-13T00:00:00", "leaveTypeCode": "ANNUAL",
    "reason": "[DEMO] về quê", "requestStatus": "Pending", "approvalRequestId": "…", "submittedAt": "…" },
  "employeeCode": "DEMOB26B-NV2", "employeeName": "[DEMO] Trần Thị Bình", "leaveTypeName": "Nghỉ phép năm", "statusName": "Chờ duyệt", "days": 2, "approvalRequestNo": "XN-261117-…",
  "pendingApproval": { "id": "…", "requestNo": "XN-…", "stepNo": 1, "stepCount": 1, "screenId": "hr-leave", "stepName": "Duyệt – Đơn xin nghỉ", "dueAt": "…", "isOverdue": false },
  "listAffectedAssignment": [ { "id": "…", "workDate": "2027-01-12T00:00:00", "shiftCode": "TRUA", "status": "Planned", "isDeleted": false } ],
  "listWarning": [ "Duyệt đơn sẽ bỏ ca TRUA ngày 12/01/2027 đã phân" ], "allowedActions": [ "cancel", "approve", "reject" ] }
```
Trạng thái: `Pending` Chờ duyệt → `Approved` Đã duyệt / `Rejected` Từ chối / `Cancelled` Đã hủy. `allowedActions`: `cancel` (người tạo, Chờ duyệt), `approve` / `reject` (có quyền Duyệt cấp đang chờ).

## 4. Đơn đổi ca `#/hr-swap` (Catalog 208, 209)

| Chức năng | Action | Input |
|---|---|---|
| Lập đơn + gửi duyệt | `POST ShiftSwapRequest/Create` | `CurrentShiftAssignmentId` (ca của người xin), `ProposedShiftAssignmentId?` (ca của người nhận → **hoán đổi**), `TargetEmployeeId?` (không có ca đề xuất → **nhường ca**, bắt buộc), `Reason` |
| Chi tiết | `GET ShiftSwapRequest/GetDetail?id=` | |
| Đơn của tôi | `GET ShiftSwapRequest/GetMine` | (người tạo / người xin / người nhận) |
| Duyệt / từ chối / hủy | `POST ShiftSwapRequest/Approve` · `Reject` · `Cancel` | như đơn nghỉ |

Số đơn `PH-YYMMDD-NNNN`. Kiểm (lúc lập và lúc duyệt): 2 ca còn hiệu lực, Kế hoạch / Xác nhận, **chưa chấm công**, không nằm trong đơn khác chờ duyệt; sau khi đổi mỗi người không có 2 ca / ngày,
không trùng ngày nghỉ phép. Duyệt → hoán đổi người của 2 ca (nhường: chuyển ca cho người nhận) – Note "Đổi ca theo đơn PH-…". `swapType`: `SWAP` / `GIVE`.
Người nhận đồng ý: ngoài hệ thống (người duyệt xác nhận) – PROPOSED D-55 (i). Chi tiết có `currentAssignment`, `proposedAssignment`, `requesterName`, `targetName`, `pendingApproval`,
`listWarning` ("Duyệt sẽ không thực hiện được: …" khi điều kiện đã đổi), `allowedActions`.

## 5. Hộp duyệt `#/wf-inbox`
`GET ApprovalRequest/GetMyPending?type=hr-leave` / `type=hr-swap`: tiêu đề "Đơn nghỉ DN-…: <nhân viên> – <loại> – dd/MM/yyyy → dd/MM/yyyy (n ngày) – bỏ k ca đã phân",
"Đổi ca PH-…: A (TRUA 13/01/2027) ↔ B (TOI 17/01/2027)", "Nhường ca PH-…: A → B (TRUA 13/01/2027)". `ApprovalRequest/GetDetail` → `business.table` = `LeaveRequest` / `ShiftSwapRequest`,
`business.lines` = các ca liên quan.
