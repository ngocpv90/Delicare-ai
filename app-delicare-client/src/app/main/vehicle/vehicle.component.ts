import { Component } from '@angular/core';
import { map } from 'rxjs';
import { VEHICLE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { VehicleService } from 'src/app/services/vehicle.service';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { VehicleModalComponent } from './vehicle-modal/vehicle-modal.component';

// log-vehicle – Xe giao hàng giao hàng (FE-2).
@Component({
  selector: 'app-vehicle',
  templateUrl: './vehicle.component.html',
})
export class VehicleComponent extends MasterListBase {
  public readonly statuses = VEHICLE_STATUSES;
  public readonly labelOf = labelOf;
  public listType: { code: string; label: string }[] = [];
  protected searchFields = ['code', 'licensePlate', 'vehicleType', 'note'];
  protected override filterDefaults = { vehicleType: null, status: '' };

  constructor(private vehicleService: VehicleService) {
    super();
  }

  protected loadRows() {
    return this.vehicleService.getAllData().pipe(
      map((res) => {
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        // VehicleType is free text: the filter offers the values in use
        this.listType = [...new Set(list.map((x) => x.vehicleType).filter((x) => !!x))]
          .sort((a, b) => a.localeCompare(b, 'vi'))
          .map((x) => ({ code: x, label: x }));
        return list;
      })
    );
  }

  protected deleteRow(id: any) {
    return this.vehicleService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(VehicleModalComponent, data);
  }
}
