import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ACTIVE_STATUSES, LOCATION_TYPES } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { StorageLocationService } from 'src/app/services/storage-location.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// StorageLocationRequest: WarehouseId, StorageZoneId, Code, Name, LocationType, Status.
@Component({
  selector: 'app-storage-location-modal',
  templateUrl: './storage-location-modal.component.html',
})
export class StorageLocationModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly locationTypes = LOCATION_TYPES;
  public listWarehouse: any[] = [];
  public listZone: any[] = [];
  public zones: any[] = [];
  protected requestFields = ['warehouseId', 'storageZoneId', 'code', 'name', 'locationType', 'status'];

  constructor(
    protected api: StorageLocationService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      warehouseId: [null, [Validators.required]],
      storageZoneId: [null],
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      locationType: [null],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    forkJoin([this.lookup.warehouses(), this.lookup.storageZones()]).subscribe(([warehouses, zones]) => {
      this.listWarehouse = warehouses;
      this.listZone = zones;
      this.refreshZones();
      this.isLoadingList = false;
    });
  }

  // Zones of the picked warehouse (kept as a field: app-select re-renders when its items reference changes)
  private refreshZones(): void {
    const warehouseId = this.formGroup.value.warehouseId;
    this.zones = warehouseId ? this.listZone.filter((x) => x.warehouseId === warehouseId) : [];
  }

  protected override patchRecord(record: any): void {
    super.patchRecord(record);
    this.refreshZones();
  }

  public onChangeWarehouse(): void {
    this.refreshZones();
    const zone = this.listZone.find((x) => x.id === this.formGroup.value.storageZoneId);
    if (zone && zone.warehouseId !== this.formGroup.value.warehouseId) {
      this.formGroup.patchValue({ storageZoneId: null });
    }
  }

  protected override toParams(v: any) {
    return { ...v, code: (v.code ?? '').trim(), name: (v.name ?? '').trim() };
  }
}
