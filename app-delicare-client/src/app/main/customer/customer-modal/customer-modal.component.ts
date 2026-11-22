import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { CodeLabel } from 'src/app/helpers/master-data-options';
import { CustomerService } from 'src/app/services/customer.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// CustomerRequest has 40 fields (individual + corporate profile, CRM assignment, external data). The form edits
// the ones the catering business uses; every other field is sent back unchanged from GetById (Update overwrites).
// Phone / PhoneNumber are duplicates: both written with the same value. DisplayName (shown by the SPs) defaults to Name.
@Component({
  selector: 'app-customer-modal',
  templateUrl: './customer-modal.component.html',
})
export class CustomerModalComponent extends MasterModalBase {
  public statuses: CodeLabel[] = [];
  public customerTypes: CodeLabel[] = [];
  public listBranch: any[] = [];
  public listCompany: any[] = [];
  protected requestFields = [
    'companyId', 'code', 'name', 'taxCode', 'phone', 'email', 'address', 'customerType', 'status', 'note', 'cif',
    'displayName', 'phoneNumber', 'firstName', 'lastName', 'gender', 'dateOfBirth', 'identityNumber', 'identityDate',
    'identityPlace', 'occupation', 'legalName', 'businessRegistrationNumber', 'registrationDate', 'registrationPlace',
    'companySize', 'industry', 'legalRepresentativeName', 'contactPersonName', 'contactPersonPhone',
    'contactPersonEmail', 'source', 'regionId', 'areaId', 'branchId', 'departmentId', 'userAssignId',
    'externalData', 'externalValue',
  ];

  constructor(
    protected api: CustomerService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      displayName: [''],
      legalName: [''],
      customerType: [null, [Validators.required]],
      taxCode: [''],
      businessRegistrationNumber: [''],
      phone: [''],
      email: ['', [Validators.email]],
      address: [''],
      legalRepresentativeName: [''],
      contactPersonName: [''],
      contactPersonPhone: [''],
      contactPersonEmail: ['', [Validators.email]],
      branchId: [null],
      companyId: [null],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getStatusSet('Customer.Status').subscribe((list) => (this.statuses = list));
    this.metaDataService.getReferenceList('CustomerType').subscribe((list) => (this.customerTypes = list));
    forkJoin([this.lookup.branches(), this.lookup.companies()]).subscribe(([branches, companies]) => {
      this.listBranch = branches;
      this.listCompany = companies;
      if (!this.isEdit && companies.length === 1 && !this.formGroup.value.companyId) {
        this.formGroup.patchValue({ companyId: companies[0].id });
      }
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue({ ...record, phone: record.phoneNumber || record.phone || '' });
  }

  protected override toParams(v: any) {
    const name = (v.name ?? '').trim();
    const phone = (v.phone ?? '').trim();
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name,
      displayName: (v.displayName ?? '').trim() || name,
      phone,
      phoneNumber: phone,
      email: (v.email ?? '').trim(),
      contactPersonEmail: (v.contactPersonEmail ?? '').trim(),
    };
  }
}
