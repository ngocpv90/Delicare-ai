import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES } from 'src/app/helpers/master-data-options';
import { CustomerContactService } from 'src/app/services/customer-contact.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// CustomerContactRequest: CustomerId, FullName, Position, Phone, Email, IsPrimary, Note, Name, Status.
// Name is a duplicate of FullName (the SP searches both): written with the same value.
@Component({
  selector: 'app-customer-contact-modal',
  templateUrl: './customer-contact-modal.component.html',
})
export class CustomerContactModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listCustomer: any[] = [];
  protected requestFields = ['customerId', 'fullName', 'position', 'phone', 'email', 'isPrimary', 'note', 'name', 'status'];

  constructor(
    protected api: CustomerContactService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      customerId: [null, [Validators.required]],
      fullName: ['', [Validators.required]],
      position: [''],
      phone: [''],
      email: ['', [Validators.email]],
      isPrimary: [false],
      status: ['Active', [Validators.required]],
      note: [''],
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
    this.formGroup.patchValue({ ...record, fullName: record.fullName || record.name });
  }

  protected override toParams(v: any) {
    const fullName = (v.fullName ?? '').trim();
    return { ...v, fullName, name: fullName, email: (v.email ?? '').trim(), isPrimary: !!v.isPrimary };
  }
}
