import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, LOCATION_TYPES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { StorageLocationService } from 'src/app/services/storage-location.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { StorageLocationModalComponent } from './storage-location-modal/storage-location-modal.component';

// scm-location – Vị trí lưu trữ (bin) theo kho / khu (FE-2).
@Component({
  selector: 'app-storage-location',
  templateUrl: './storage-location.component.html',
})
export class StorageLocationComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly locationTypes = LOCATION_TYPES;
  public readonly labelOf = labelOf;
  public listWarehouse: any[] = [];
  public listZone: any[] = [];
  // zones of the warehouse picked in the filter
  public filterZones: any[] = [];
  protected searchFields = ['code', 'name', 'warehouseName', 'storageZoneName'];
  protected override filterDefaults = { warehouseId: null, storageZoneId: null, locationType: null, status: '' };

  constructor(
    private storageLocationService: StorageLocationService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  private refreshZones(): void {
    const warehouseId = this.searchForm.value.warehouseId;
    this.filterZones = warehouseId ? this.listZone.filter((x) => x.warehouseId === warehouseId) : this.listZone;
  }

  public onChangeWarehouse(): void {
    this.refreshZones();
    const zone = this.listZone.find((x) => x.id === this.searchForm.value.storageZoneId);
    if (zone && zone.warehouseId !== this.searchForm.value.warehouseId) {
      this.searchForm.patchValue({ storageZoneId: null });
    }
    this.search();
  }

  protected loadRows() {
    return forkJoin([
      this.storageLocationService.getAllData(),
      this.lookup.warehouses(),
      this.lookup.storageZones(),
    ]).pipe(
      map(([res, warehouses, zones]) => {
        this.listWarehouse = warehouses;
        this.listZone = zones;
        this.refreshZones();
        const warehouseNames = labelMap(warehouses);
        const zoneNames = labelMap(zones);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          warehouseName: labelFrom(warehouseNames, x.warehouseId),
          storageZoneName: labelFrom(zoneNames, x.storageZoneId),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.storageLocationService.delete(id);
  }

  public override resetSearch(): void {
    super.resetSearch();
    this.refreshZones();
  }

  public openModal(data?: any): void {
    const { warehouseId, storageZoneId } = this.searchForm.value;
    this.openForm(StorageLocationModalComponent, data ?? { warehouseId, storageZoneId });
  }
}
