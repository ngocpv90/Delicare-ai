import { Component, Input } from '@angular/core';
import { OrderDetail } from './account.service';

/** Ngày giờ máy chủ (giờ Việt Nam, không múi giờ) → "HH:mm dd/MM". */
export function viTime(s?: string | null, withDate = true): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})/.exec(s || '');
  if (!m) return '';
  return withDate ? `${m[4]}:${m[5]} ${m[3]}/${m[2]}` : `${m[4]}:${m[5]}`;
}

export function viDate(s?: string | null): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s || '');
  return m ? `${m[3]}/${m[2]}/${m[1]}` : s || '';
}

/**
 * RETAIL-2: tiến độ đơn (Nhận đơn → Xác nhận → Sơ chế → Nấu → Đóng gói → Giao hàng → Đã giao) + thẻ chuyến giao.
 * Dùng chung cho "Tra cứu đơn" và "Đơn của tôi". Dữ liệu từ máy chủ (RetailPortal/TrackOrder, GetMyOrder).
 */
@Component({
  selector: 'app-order-progress',
  standalone: true,
  template: `
    @if (o) {
      <div class="pt-stage" [class.cancel]="o.stageCode === 'CANCELLED'" [class.ok]="o.stageCode === 'DELIVERED'">
        <i class="material-icons">{{ icon(o.stageCode) }}</i>
        <div>
          <small>Đơn đang ở bước</small>
          <strong>{{ o.stageLabel }}</strong>
        </div>
      </div>
      <ol class="pt-timeline">
        @for (s of o.listStep; track s.code) {
          <li [class]="s.state">
            <span class="dot"><i class="material-icons">{{ s.state === 'done' ? 'check' : s.state === 'cancelled' ? 'close' : icon(s.code) }}</i></span>
            <div>
              <b>{{ s.label }}</b>
              @if (s.at) { <small>{{ time(s.at) }}</small> }
              @if (s.state === 'current') { <em>Đang thực hiện</em> }
              @if (s.note) { <small class="pt-muted">{{ s.note }}</small> }
            </div>
          </li>
        }
      </ol>

      @if (o.route; as r) {
        <section class="pt-route">
          <header>
            <h3><i class="material-icons">local_shipping</i>Chuyến giao {{ r.routeNumber }}</h3>
            <span class="pt-tag">{{ r.statusLabel }}</span>
          </header>
          <dl>
            @if (r.licensePlate) { <div><dt>Xe</dt><dd>{{ r.licensePlate }}</dd></div> }
            @if (r.driverName) { <div><dt>Tài xế</dt><dd>{{ r.driverName }}</dd></div> }
            <div><dt>Xuất phát</dt><dd>{{ r.actualStart ? time(r.actualStart) : r.plannedStart ? 'Dự kiến ' + time(r.plannedStart) : '—' }}</dd></div>
            @if (r.plannedEnd && !r.actualEnd) { <div><dt>Dự kiến xong chuyến</dt><dd>{{ time(r.plannedEnd) }}</dd></div> }
            @if (r.actualEnd) { <div><dt>Hoàn thành chuyến</dt><dd>{{ time(r.actualEnd) }}</dd></div> }
            @if (r.stopCount) { <div><dt>Tiến độ chuyến</dt><dd>Đã giao {{ r.stopDoneCount }}/{{ r.stopCount }} điểm</dd></div> }
          </dl>
          @if (r.stopCount) {
            <div class="pt-bar"><span [style.width.%]="(r.stopDoneCount * 100) / r.stopCount"></span></div>
          }
          @if (r.lastLatitude != null && r.lastLongitude != null) {
            <a class="pt-btn pt-btn-sm pt-btn-ghost" target="_blank" rel="noopener" [href]="'https://www.google.com/maps?q=' + r.lastLatitude + ',' + r.lastLongitude">
              <i class="material-icons">place</i>Vị trí xe lúc {{ time(r.lastPositionAt, false) }} – xem bản đồ
            </a>
          }
          @if (r.listEvent.length) {
            <ul class="pt-route-events">
              @for (e of r.listEvent; track $index) {
                <li><span>{{ time(e.at) }}</span>{{ e.label }}@if (e.temperatureC != null) { · {{ e.temperatureC }}°C }</li>
              }
            </ul>
          }
        </section>
      } @else if (o.stageCode === 'DELIVERING') {
        <p class="pt-muted"><i class="material-icons pt-inline-icon">local_shipping</i>Shipper đang trên đường giao – nhân viên sẽ gọi khi đến nơi.</p>
      }
    }
  `,
})
export class OrderProgressComponent {
  @Input() public o: OrderDetail | null = null;

  public time(s?: string | null, withDate = true): string {
    return viTime(s, withDate);
  }

  public icon(code: string): string {
    switch (code) {
      case 'RECEIVED': return 'receipt_long';
      case 'CONFIRMED': return 'task_alt';
      case 'PREP': return 'content_cut';
      case 'COOKING': return 'soup_kitchen';
      case 'PACKING': return 'inventory_2';
      case 'DELIVERING': return 'local_shipping';
      case 'DELIVERED': return 'check_circle';
      case 'CANCELLED': return 'cancel';
      default: return 'radio_button_unchecked';
    }
  }
}
