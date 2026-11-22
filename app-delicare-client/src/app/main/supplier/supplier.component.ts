import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, labelOf } from 'src/app/helpers/master-data-options';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { SupplierService } from 'src/app/services/supplier.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { SupplierModalComponent } from './supplier-modal/supplier-modal.component';

// scm-supplier – Nhà cung cấp. FE-2: list from Supplier/GetAllData (uspSupplier_GetAll only returns
// Id / Name / Note) + ngăn kéo tổng quan (Supplier/GetOverview: bảng giá, PO đang mở, sự cố, tỷ lệ 90 ngày).
@Component({
  selector: 'app-supplier',
  templateUrl: './supplier.component.html',
})
export class SupplierComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public supplierTypes: CodeLabel[] = [];
  protected searchFields = ['code', 'name', 'taxCode', 'phone', 'contactName', 'contactPhone', 'email'];
  protected override filterDefaults = { supplierType: null, status: '' };

  // Drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'overview';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'overview', label: 'Tổng quan' },
    { key: 'price', label: 'Bảng giá' },
    { key: 'po', label: 'PO đang mở' },
    { key: 'issue', label: 'Sự cố' },
  ];
  public overview: any;
  public isLoadingOverview = false;

  constructor(
    private supplierService: SupplierService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.metaDataService.getReferenceList('SupplierType').subscribe((list) => (this.supplierTypes = list));
  }

  protected loadRows() {
    return forkJoin([this.supplierService.getAllData()]).pipe(
      map(([res]) => (res?.responseCode === 200 ? res.listData ?? [] : []))
    );
  }

  protected deleteRow(id: any) {
    return this.supplierService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(SupplierModalComponent, data);
  }

  public openDrawer(row: any): void {
    this.drawerRow = row;
    this.drawerTab = 'overview';
    this.drawerOpen = true;
    this.overview = null;
    this.isLoadingOverview = true;
    this.supplierService.getOverview(row.id).subscribe({
      next: (res) => {
        this.isLoadingOverview = false;
        if (res?.responseCode === 200) {
          this.overview = res.data;
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: () => {
        this.isLoadingOverview = false;
        this.notify.error();
      },
    });
  }

  public editFromDrawer(): void {
    this.drawerOpen = false;
    this.openModal(this.drawerRow);
  }
}
