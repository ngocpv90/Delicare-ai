# BOT-1.5b – Bot Vận hành (Giao vận, Điểm phát) – API mới cho dev FE

| API | Quyền | Vào / ra | Ghi chú |
|---|---|---|---|
| `GET api/DeliveryRoute/GetMine?deliveryDate=yyyy-MM-dd` | Chỉ cần đăng nhập (chuyến của chính mình) | Giống `DeliveryRoute/GetBoard`: `data = { deliveryDate, listRoute[] = { route, statusName, licensePlate, driverName, loadMeals, departureTemperatureC, listStop[], allowedActions[] }, statusCounts }`; bỏ `deliveryDate` = hôm nay | Chuyến có tài xế = nhân viên của tài khoản. Tài khoản chưa gắn nhân viên → lỗi "Tài khoản chưa gắn với nhân viên…". FE dùng được cho màn "Chuyến của tôi" trên điện thoại. |

Không đổi hành vi API cũ. Bot dùng lại `DeliveryRoute/GetBoard|GetDetail|Dispatch`, `DeliveryStop/GetById|Arrive|Deliver|Fail`, `ContainerReturn/Record`, `OrderReconciliation/GetPendingOrders|Reconcile` bằng JWT của chính người dùng – dữ liệu ghi qua bot hiện y như ghi trên web (ghi chú / lý do có chữ "Telegram").

Không có bảng / cột mới – sau `git pull` chỉ cần build lại API (DB không đổi).
