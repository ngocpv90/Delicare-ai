import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, toNumberParam } from 'src/app/helpers/master-data-options';
import { IngredientService } from 'src/app/services/ingredient.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// IngredientRequest: CompanyId, IngredientCategoryId, BaseUnitId, Code, Name, Specification, StorageCondition,
// IsExpiryManaged, ShelfLifeDays, MinimumStock, SafetyStock, Status, Note, PurchaseUomId, IssueUomId,
// StorageType, YieldPercent, StandardWastePercent, LotControl, ExpiryControl, QcRequired.
// "Quản lý hạn dùng" writes IsExpiryManaged and ExpiryControl together (duplicate flags).
@Component({
  selector: 'app-ingredient-modal',
  templateUrl: './ingredient-modal.component.html',
})
export class IngredientModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public storageTypes: CodeLabel[] = [];
  public listCategory: any[] = [];
  public listUnit: any[] = [];
  public listCompany: any[] = [];
  protected requestFields = [
    'companyId', 'ingredientCategoryId', 'baseUnitId', 'code', 'name', 'specification', 'storageCondition',
    'isExpiryManaged', 'shelfLifeDays', 'minimumStock', 'safetyStock', 'status', 'note', 'purchaseUomId',
    'issueUomId', 'storageType', 'yieldPercent', 'standardWastePercent', 'lotControl', 'expiryControl', 'qcRequired',
  ];

  constructor(
    protected api: IngredientService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      ingredientCategoryId: [null, [Validators.required]],
      specification: [''],
      baseUnitId: [null, [Validators.required]],
      purchaseUomId: [null],
      issueUomId: [null],
      storageType: [null],
      storageCondition: [''],
      isExpiryManaged: [true],
      shelfLifeDays: [null, [Validators.min(0)]],
      lotControl: [false],
      qcRequired: [false],
      minimumStock: [null, [Validators.min(0)]],
      safetyStock: [null, [Validators.min(0)]],
      yieldPercent: [null, [Validators.min(0), Validators.max(100)]],
      standardWastePercent: [null, [Validators.min(0), Validators.max(100)]],
      companyId: [null],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getReferenceList('StorageType').subscribe((list) => (this.storageTypes = list));
    forkJoin([this.lookup.ingredientCategories(), this.lookup.units(), this.lookup.companies()]).subscribe(
      ([categories, units, companies]) => {
        this.listCategory = categories;
        this.listUnit = units;
        this.listCompany = companies;
        if (!this.isEdit && companies.length === 1 && !this.formGroup.value.companyId) {
          this.formGroup.patchValue({ companyId: companies[0].id });
        }
        this.isLoadingList = false;
      }
    );
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue({
      ...record,
      isExpiryManaged: !!(record.isExpiryManaged || record.expiryControl),
      lotControl: !!record.lotControl,
      qcRequired: !!record.qcRequired,
    });
  }

  protected override validate(v: any): string | null {
    if (v.minimumStock !== null && v.safetyStock !== null && Number(v.minimumStock) > Number(v.safetyStock)) {
      return 'Tồn tối thiểu phải nhỏ hơn hoặc bằng tồn an toàn';
    }
    return null;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      isExpiryManaged: !!v.isExpiryManaged,
      expiryControl: !!v.isExpiryManaged,
      lotControl: !!v.lotControl,
      qcRequired: !!v.qcRequired,
      shelfLifeDays: toNumberParam(v.shelfLifeDays),
      minimumStock: toNumberParam(v.minimumStock),
      safetyStock: toNumberParam(v.safetyStock),
      yieldPercent: toNumberParam(v.yieldPercent),
      standardWastePercent: toNumberParam(v.standardWastePercent),
    };
  }
}
