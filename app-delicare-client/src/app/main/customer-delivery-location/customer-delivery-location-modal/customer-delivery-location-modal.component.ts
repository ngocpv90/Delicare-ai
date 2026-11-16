import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES, timeText, toNumberParam, toTimeParam } from 'src/app/helpers/master-data-options';
import { CustomerDeliveryLocationService } from 'src/app/services/customer-delivery-location.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// CustomerDeliveryLocationRequest: CustomerId, Code, Name, Address, ContactName, ContactPhone, DeliveryInstructions,
// Status, Latitude, Longitude, DeliveryWindowFrom, DeliveryWindowTo (TimeSpan "HH:mm:ss").
@Component({
  selector: 'app-customer-delivery-location-modal',
  templateUrl: './customer-delivery-location-modal.component.html',
})
export class CustomerDeliveryLocationModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listCustomer: any[] = [];
  protected requestFields = [
    'customerId', 'code', 'name', 'address', 'contactName', 'contactPhone', 'deliveryInstructions', 'status',
    'latitude', 'longitude', 'deliveryWindowFrom', 'deliveryWindowTo',
  ];

  constructor(
    protected api: CustomerDeliveryLocationService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      customerId: [null, [Validators.required]],
      code: ['', [Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      address: ['', [Validators.required]],
      deliveryWindowFrom: [''],
      deliveryWindowTo: [''],
      contactName: [''],
      contactPhone: [''],
      latitude: [null, [Validators.min(-90), Validators.max(90)]],
      longitude: [null, [Validators.min(-180), Validators.max(180)]],
      deliveryInstructions: [''],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.lookup.customers().subscribe((list) => {
      this.listCustomer = list;
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue({
      ...record,
      deliveryWindowFrom: timeText(record.deliveryWindowFrom),
      deliveryWindowTo: timeText(record.deliveryWindowTo),
    });
  }

  protected override validate(v: any): string | null {
    if (v.deliveryWindowFrom && v.deliveryWindowTo && v.deliveryWindowFrom >= v.deliveryWindowTo) {
      return 'Giờ nhận đến phải sau giờ nhận từ';
    }
    if ((v.latitude === null) !== (v.longitude === null)) {
      return 'Nhập đủ cả vĩ độ và kinh độ (hoặc bỏ trống cả hai)';
    }
    return null;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      deliveryWindowFrom: toTimeParam(v.deliveryWindowFrom),
      deliveryWindowTo: toTimeParam(v.deliveryWindowTo),
      latitude: toNumberParam(v.latitude),
      longitude: toNumberParam(v.longitude),
    };
  }
}
