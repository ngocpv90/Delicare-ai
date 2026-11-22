import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, labelOf, timeText } from 'src/app/helpers/master-data-options';
import { CustomerService } from 'src/app/services/customer.service';
import { MasterLookupService, customerName } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { CustomerModalComponent } from './customer-modal/customer-modal.component';

// crm-customer – Khách hàng (FE-2). List: Customer/GetAllData + hợp đồng hiệu lực / số điểm giao
// (Contract, CustomerDeliveryLocation GetAllData). Ngăn kéo 360°: Customer/GetOverview.
@Component({
  selector: 'app-customer',
  templateUrl: './customer.component.html',
})
export class CustomerComponent extends MasterListBase {
  public readonly labelOf = labelOf;
  public readonly timeText = timeText;
  public readonly recordStatuses = ACTIVE_STATUSES;
  public statuses: CodeLabel[] = [];
  public customerTypes: CodeLabel[] = [];
  public mealTypes: CodeLabel[] = [];
  public listBranch: any[] = [];
  protected searchFields = ['code', 'customerName', 'name', 'legalName', 'taxCode', 'phoneNumber', 'phone', 'contactPersonName'];
  protected override filterDefaults = { customerType: null, branchId: null, status: '' };

  // Drawer 360°
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'overview';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'overview', label: 'Tổng quan' },
    { key: 'contact', label: 'Liên hệ & điểm giao' },
    { key: 'contract', label: 'Hợp đồng & suất ăn' },
    { key: 'order', label: 'Đơn hàng gần đây' },
  ];
  public overview: any;
  public isLoadingOverview = false;

  constructor(
    private customerService: CustomerService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.metaDataService.getStatusSet('Customer.Status').subscribe((list) => (this.statuses = list));
    this.metaDataService.getReferenceList('CustomerType').subscribe((list) => (this.customerTypes = list));
    this.metaDataService.getReferenceList('MealType').subscribe((list) => (this.mealTypes = list));
  }

  protected loadRows() {
    return forkJoin([
      this.customerService.getAllData(),
      this.lookup.organizationUnits(),
      this.lookup.contracts(),
      this.lookup.list('CustomerDeliveryLocation'),
      this.lookup.branches(),
    ]).pipe(
      map(([res, units, contracts, locations, branches]) => {
        this.listBranch = branches;
        const unitNames = labelMap(units, (x) => x.name);
        const byCustomer = (list: any[], id: string) =>
          list.filter((x) => String(x.customerId).toLowerCase() === String(id).toLowerCase());
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          customerName: customerName(x),
          branchName: labelFrom(unitNames, x.branchId),
          activeContracts: byCustomer(contracts, x.id)
            .filter((c) => (c.status ?? '').toLowerCase() === 'active')
            .map((c) => c.contractNumber || c.name)
            .join(', '),
          locationCount: byCustomer(locations, x.id).length,
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.customerService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(CustomerModalComponent, data, 'xl');
  }

  public openDrawer(row: any): void {
    this.drawerRow = row;
    this.drawerTab = 'overview';
    this.drawerOpen = true;
    this.overview = null;
    this.isLoadingOverview = true;
    this.customerService.getOverview(row.id).subscribe({
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
