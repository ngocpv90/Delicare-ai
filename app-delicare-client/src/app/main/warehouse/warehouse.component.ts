import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, LOCATION_TYPES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { WarehouseService } from 'src/app/services/warehouse.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { WarehouseModalComponent } from './warehouse-modal/warehouse-modal.component';

// scm-warehouse – Danh mục kho (FE-2) + ngăn kéo "Sơ đồ kho" (Warehouse/GetLayout: khu bảo quản -> vị trí).
@Component({
  selector: 'app-warehouse',
  templateUrl: './warehouse.component.html',
})
export class WarehouseComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly locationTypes = LOCATION_TYPES;
  public readonly labelOf = labelOf;
  public warehouseTypes: CodeLabel[] = [];
  public listUnit: any[] = [];
  protected searchFields = ['code', 'name', 'address', 'organizationUnitName'];
  protected override filterDefaults = { warehouseType: null, organizationUnitId: null, status: '' };

  // Drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'layout';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'layout', label: 'Sơ đồ kho' },
    { key: 'info', label: 'Thông tin' },
  ];
  public layout: any;
  public isLoadingLayout = false;

  constructor(
    private warehouseService: WarehouseService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.metaDataService.getReferenceList('WarehouseType').subscribe((list) => (this.warehouseTypes = list));
  }

  protected loadRows() {
    return forkJoin([
      this.warehouseService.getAllData(),
      this.lookup.organizationUnits(),
      this.lookup.storageZones(),
      this.lookup.list('StorageLocation'),
    ]).pipe(
      map(([res, units, zones, locations]) => {
        this.listUnit = units;
        const unitNames = labelMap(units);
        const count = (list: any[], id: string) =>
          list.filter((x) => String(x.warehouseId).toLowerCase() === String(id).toLowerCase()).length;
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          organizationUnitName: labelFrom(unitNames, x.organizationUnitId),
          zoneCount: count(zones, x.id),
          locationCount: count(locations, x.id),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.warehouseService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(WarehouseModalComponent, data);
  }

  public openDrawer(row: any): void {
    this.drawerRow = row;
    this.drawerTab = 'layout';
    this.drawerOpen = true;
    this.layout = null;
    this.isLoadingLayout = true;
    this.warehouseService.getLayout(row.id).subscribe({
      next: (res) => {
        this.isLoadingLayout = false;
        if (res?.responseCode === 200) {
          this.layout = res.data;
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: () => {
        this.isLoadingLayout = false;
        this.notify.error();
      },
    });
  }

  public editFromDrawer(): void {
    this.drawerOpen = false;
    this.openModal(this.drawerRow);
  }
}
