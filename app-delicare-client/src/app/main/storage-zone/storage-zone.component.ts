import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { StorageZoneService } from 'src/app/services/storage-zone.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { StorageZoneModalComponent } from './storage-zone-modal/storage-zone-modal.component';

// scm-zone – Khu bảo quản (FE-2).
@Component({
  selector: 'app-storage-zone',
  templateUrl: './storage-zone.component.html',
})
export class StorageZoneComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public listWarehouse: any[] = [];
  protected searchFields = ['code', 'name', 'warehouseName', 'temperatureRange'];
  protected override filterDefaults = { warehouseId: null, status: '' };

  constructor(
    private storageZoneService: StorageZoneService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([
      this.storageZoneService.getAllData(),
      this.lookup.warehouses(),
      this.lookup.list('StorageLocation'),
    ]).pipe(
      map(([res, warehouses, locations]) => {
        this.listWarehouse = warehouses;
        const names = labelMap(warehouses);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          warehouseName: labelFrom(names, x.warehouseId),
          locationCount: locations.filter((l) => String(l.storageZoneId).toLowerCase() === String(x.id).toLowerCase()).length,
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.storageZoneService.delete(id);
  }

  public openModal(data?: any): void {
    // new zone: preset the warehouse being filtered
    this.openForm(StorageZoneModalComponent, data ?? { warehouseId: this.searchForm.value.warehouseId });
  }
}
