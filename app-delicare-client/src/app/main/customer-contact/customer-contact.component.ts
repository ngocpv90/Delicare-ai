import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { CustomerContactService } from 'src/app/services/customer-contact.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { CustomerContactModalComponent } from './customer-contact-modal/customer-contact-modal.component';

// crm-contact – Đầu mối liên hệ khách hàng (FE-2).
@Component({
  selector: 'app-customer-contact',
  templateUrl: './customer-contact.component.html',
})
export class CustomerContactComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public listCustomer: any[] = [];
  protected searchFields = ['fullName', 'name', 'position', 'phone', 'email', 'customerName'];
  protected override filterDefaults = { customerId: null, primaryOnly: false, status: '' };
  protected override customFilters = ['primaryOnly'];

  constructor(
    private customerContactService: CustomerContactService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.customerContactService.getAllData(), this.lookup.customers()]).pipe(
      map(([res, customers]) => {
        this.listCustomer = customers;
        const names = labelMap(customers);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({ ...x, fullName: x.fullName || x.name, customerName: labelFrom(names, x.customerId) }));
      })
    );
  }

  protected override matches(row: any, filter: any): boolean {
    return !filter.primaryOnly || !!row.isPrimary;
  }

  protected deleteRow(id: any) {
    return this.customerContactService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(CustomerContactModalComponent, data ?? { customerId: this.searchForm.value.customerId });
  }
}
