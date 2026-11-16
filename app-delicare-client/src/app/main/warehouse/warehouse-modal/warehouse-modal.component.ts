import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { WarehouseService } from 'src/app/services/warehouse.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// WarehouseRequest: CompanyId, OrganizationUnitId, Code, Name, WarehouseType, Address, Status, Note.
@Component({
  selector: 'app-warehouse-modal',
  templateUrl: './warehouse-modal.component.html',
})
export class WarehouseModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public warehouseTypes: CodeLabel[] = [];
  public listCompany: any[] = [];
  public listUnit: any[] = [];
  protected requestFields = ['companyId', 'organizationUnitId', 'code', 'name', 'warehouseType', 'address', 'status', 'note'];

  constructor(
    protected api: WarehouseService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      companyId: [null],
      organizationUnitId: [null],
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      warehouseType: [null, [Validators.required]],
      address: [''],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getReferenceList('WarehouseType').subscribe((list) => (this.warehouseTypes = list));
    forkJoin([this.lookup.companies(), this.lookup.organizationUnits()]).subscribe(([companies, units]) => {
      this.listCompany = companies;
      this.listUnit = units;
      if (!this.isEdit && companies.length === 1 && !this.formGroup.value.companyId) {
        this.formGroup.patchValue({ companyId: companies[0].id });
      }
      this.isLoadingList = false;
    });
  }

  // Unit picked -> company of that unit
  public onChangeUnit(id: any): void {
    const unit = this.listUnit.find((x) => x.id === id);
    if (unit?.companyId) {
      this.formGroup.patchValue({ companyId: unit.companyId });
    }
  }

  protected override toParams(v: any) {
    return { ...v, code: (v.code ?? '').trim(), name: (v.name ?? '').trim() };
  }
}
