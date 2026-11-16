import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES, toNumberParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { StorageZoneService } from 'src/app/services/storage-zone.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// StorageZoneRequest: WarehouseId, Code, Name, TemperatureRange, Status, TemperatureMin, TemperatureMax.
@Component({
  selector: 'app-storage-zone-modal',
  templateUrl: './storage-zone-modal.component.html',
})
export class StorageZoneModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listWarehouse: any[] = [];
  protected requestFields = ['warehouseId', 'code', 'name', 'temperatureRange', 'status', 'temperatureMin', 'temperatureMax'];

  constructor(
    protected api: StorageZoneService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      warehouseId: [null, [Validators.required]],
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      temperatureMin: [null],
      temperatureMax: [null],
      temperatureRange: [''],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.lookup.warehouses().subscribe((list) => {
      this.listWarehouse = list;
      this.isLoadingList = false;
    });
  }

  protected override validate(v: any): string | null {
    if (v.temperatureMin !== null && v.temperatureMax !== null && Number(v.temperatureMin) > Number(v.temperatureMax)) {
      return 'Nhiệt độ min phải nhỏ hơn hoặc bằng nhiệt độ max';
    }
    return null;
  }

  protected override toParams(v: any) {
    const hasRange = v.temperatureMin !== null || v.temperatureMax !== null;
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      temperatureMin: toNumberParam(v.temperatureMin),
      temperatureMax: toNumberParam(v.temperatureMax),
      // text range for older screens / prints when left empty
      temperatureRange:
        (v.temperatureRange ?? '').trim() ||
        (hasRange ? `${v.temperatureMin ?? '…'}…${v.temperatureMax ?? '…'}°C` : ''),
    };
  }
}
