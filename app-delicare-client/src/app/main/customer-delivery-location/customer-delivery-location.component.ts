import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf, timeText } from 'src/app/helpers/master-data-options';
import { CustomerDeliveryLocationService } from 'src/app/services/customer-delivery-location.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { CustomerDeliveryLocationModalComponent } from './customer-delivery-location-modal/customer-delivery-location-modal.component';

// crm-location – Điểm giao hàng (FE-2).
@Component({
  selector: 'app-customer-delivery-location',
  templateUrl: './customer-delivery-location.component.html',
})
export class CustomerDeliveryLocationComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public readonly timeText = timeText;
  public listCustomer: any[] = [];
  protected searchFields = ['code', 'name', 'address', 'contactName', 'contactPhone', 'customerName'];
  protected override filterDefaults = { customerId: null, status: '' };

  constructor(
    private customerDeliveryLocationService: CustomerDeliveryLocationService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.customerDeliveryLocationService.getAllData(), this.lookup.customers()]).pipe(
      map(([res, customers]) => {
        this.listCustomer = customers;
        const names = labelMap(customers);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({ ...x, customerName: labelFrom(names, x.customerId) }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.customerDeliveryLocationService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(CustomerDeliveryLocationModalComponent, data ?? { customerId: this.searchForm.value.customerId });
  }
}
