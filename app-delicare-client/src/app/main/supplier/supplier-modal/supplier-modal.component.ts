import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES, CodeLabel } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { SupplierService } from 'src/app/services/supplier.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// SupplierRequest: CompanyId, Code, Name, TaxCode, Address, Phone, Email, ContactName, Status, Note,
// SupplierType, ContactPhone. FE-2: the old form only had Name / Note (Update overwrote the rest with null).
@Component({
  selector: 'app-supplier-modal',
  templateUrl: './supplier-modal.component.html',
})
export class SupplierModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public supplierTypes: CodeLabel[] = [];
  public listCompany: any[] = [];
  protected requestFields = [
    'companyId', 'code', 'name', 'taxCode', 'address', 'phone', 'email', 'contactName', 'status', 'note',
    'supplierType', 'contactPhone',
  ];

  constructor(
    protected api: SupplierService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      supplierType: [null],
      taxCode: [''],
      phone: [''],
      email: ['', [Validators.email]],
      address: [''],
      contactName: [''],
      contactPhone: [''],
      companyId: [null],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getReferenceList('SupplierType').subscribe((list) => (this.supplierTypes = list));
    this.lookup.companies().subscribe((companies) => {
      this.listCompany = companies;
      if (!this.isEdit && companies.length === 1 && !this.formGroup.value.companyId) {
        this.formGroup.patchValue({ companyId: companies[0].id });
      }
      this.isLoadingList = false;
    });
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      taxCode: (v.taxCode ?? '').trim(),
      email: (v.email ?? '').trim(),
    };
  }
}
