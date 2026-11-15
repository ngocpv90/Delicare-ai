using AppDelicare.Application.Common;
using AppDelicare.Application.MasterData;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1.MasterData
{
    /// <summary>Khách hàng 360° (spec 4.1). CRUD + danh sách mở rộng ở <c>/sales/customers</c>.</summary>
    [Route(RoutePrefix + "/sales/customers")]
    public sealed class CustomerOverviewController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Một lần gọi cho drawer 360°: khách, liên hệ, điểm giao, hợp đồng kèm suất, 15 đơn gần nhất, KPI (số đơn, doanh thu đã đối soát, số phản ánh).</summary>
        [HttpGet("{id:guid}/overview"), Permission("crm-customer", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<CustomerOverview>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Overview(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new CustomerOverviewQuery(id), ct));
    }

    /// <summary>Nhà cung cấp – tổng quan (spec 4.3). CRUD ở <c>/scm/suppliers</c>.</summary>
    [Route(RoutePrefix + "/scm/suppliers")]
    public sealed class SupplierOverviewController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>NCC, bảng giá, PO đang mở, vấn đề NCC, KPI 90 ngày (onTimeRate, rejectRate %).</summary>
        [HttpGet("{id:guid}/overview"), Permission("scm-supplier", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<SupplierOverview>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Overview(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new SupplierOverviewQuery(id), ct));
    }

    /// <summary>Sơ đồ kho (spec 4.6). CRUD ở <c>/scm/warehouses</c>.</summary>
    [Route(RoutePrefix + "/scm/warehouses")]
    public sealed class WarehouseLayoutController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Kho → khu (nhiệt độ) → vị trí.</summary>
        [HttpGet("{id:guid}/layout"), Permission("scm-warehouse", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<WarehouseLayout>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Layout(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new WarehouseLayoutQuery(id), ct));
    }

    /// <summary>Quy đổi đơn vị (spec 4.5 – UomService.Convert). CRUD ở <c>/scm/uom-conversions</c>.</summary>
    [Route(RoutePrefix + "/scm/uom-conversions")]
    public sealed class UomConvertController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Quy đổi số lượng: chiều xuôi, ngược hoặc qua đơn vị trung gian; chỉ dùng quy đổi đang hiệu lực vào ngày <c>on</c>. Không có → 422 UOM_CONVERSION_MISSING.</summary>
        [HttpGet("convert"), Permission("scm-uom-conversion", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Convert([FromQuery] decimal quantity, [FromQuery] Guid fromUnitId, [FromQuery] Guid toUnitId, [FromQuery] DateTime? on, CancellationToken ct)
            => Ok(new { quantity, fromUnitId, toUnitId, result = await dispatcher.SendAsync(new ConvertUomQuery(quantity, fromUnitId, toUnitId, on), ct) });
    }
}
