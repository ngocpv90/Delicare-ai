import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf, timeText } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { ShiftService } from 'src/app/services/shift.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { ShiftModalComponent } from './shift-modal/shift-modal.component';

// hr-shift – Danh mục ca làm việc (4 ca SANG_6H / TRUA / TOI / DEM_23H, giờ PROPOSED D-19). FE-2.
@Component({
  selector: 'app-shift',
  templateUrl: './shift.component.html',
})
export class ShiftComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public readonly timeText = timeText;
  protected searchFields = ['code', 'name', 'note'];
  protected override filterDefaults = { status: '' };

  constructor(
    private shiftService: ShiftService,
    private lookup: MasterLookupService
  ) {
    super();
    this.table.sortField = 'startTime';
    this.table.sortOrder = 'ascend';
  }

  protected loadRows() {
    return forkJoin([this.shiftService.getAllData(), this.lookup.companies()]).pipe(
      map(([res, companies]) => {
        const names = labelMap(companies);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          companyName: labelFrom(names, x.companyId),
          // ca qua đêm: giờ kết thúc nhỏ hơn giờ bắt đầu (VD 23:00 – 01:00)
          overnight: !!x.startTime && !!x.endTime && timeText(x.endTime) < timeText(x.startTime),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.shiftService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(ShiftModalComponent, data);
  }
}
