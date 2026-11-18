# BOT-1.5a – Bot Vận hành (chung, Bếp, ATTP) – API mới cho dev FE

| API | Quyền | Vào / ra | Ghi chú |
|---|---|---|---|
| `GET api/ShiftAssignment/GetMine?days=7` | Chỉ cần đăng nhập (của chính mình) | `listData[] = { assignmentId, workDate, shiftCode, shiftName, plannedStart, plannedEnd, assignmentStatus }` từ hôm nay, `days` 1–31 | Tài khoản chưa gắn nhân viên → 400 "Tài khoản chưa gắn với nhân viên…". FE dùng được cho màn "Lịch ca của tôi" trên web / điện thoại. |

Không đổi hành vi API cũ. Bot dùng lại `LeaveRequest/Create`, `ProductionBatch/GetBoard|Start|RecordCoreTemperature|RecordSampleRetained|Finish`, `FoodSafetyChecklist/GetAllData`, `FoodSafetyChecklistResult/GetRun|Record` bằng JWT của chính người dùng – dữ liệu ghi qua bot hiện y như ghi trên web (lý do đơn nghỉ có tiền tố "[Telegram]").
