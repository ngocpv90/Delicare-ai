import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { checkValidateBetweenTwoDate, toDateParam } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, toNumberParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { SupplierMaterialService } from 'src/app/services/supplier-material.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// SupplierMaterialRequest: SupplierId, IngredientId, SupplierMaterialCode, UnitPrice, MinimumOrderQuantity,
// LeadTimeDays, QualityRequirement, EffectiveFrom, EffectiveTo, IsPreferred, Status, PurchaseUomId, DeliveryFrequency.
@Component({
  selector: 'app-supplier-material-modal',
  templateUrl: './supplier-material-modal.component.html',
})
export class SupplierMaterialModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly frequencySuggestions = ['Hằng ngày', '2 lần/tuần', '3 lần/tuần', 'Hằng tuần', 'Theo đơn'];
  public listSupplier: any[] = [];
  public listIngredient: any[] = [];
  public listUnit: any[] = [];
  protected requestFields = [
    'supplierId', 'ingredientId', 'supplierMaterialCode', 'unitPrice', 'minimumOrderQuantity', 'leadTimeDays',
    'qualityRequirement', 'effectiveFrom', 'effectiveTo', 'isPreferred', 'status', 'purchaseUomId', 'deliveryFrequency',
  ];

  constructor(
    protected api: SupplierMaterialService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      supplierId: [null, [Validators.required]],
      ingredientId: [null, [Validators.required]],
      supplierMaterialCode: [''],
      purchaseUomId: [null],
      unitPrice: [null, [Validators.required, Validators.min(0)]],
      minimumOrderQuantity: [null, [Validators.min(0)]],
      leadTimeDays: [null, [Validators.min(0)]],
      deliveryFrequency: [''],
      effectiveFrom: [null],
      effectiveTo: [null],
      isPreferred: [false],
      qualityRequirement: [''],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    forkJoin([this.lookup.suppliers(), this.lookup.ingredients(), this.lookup.units()]).subscribe(
      ([suppliers, ingredients, units]) => {
        this.listSupplier = suppliers;
        this.listIngredient = ingredients;
        this.listUnit = units;
        this.isLoadingList = false;
      }
    );
  }

  // New price line: default the purchase unit from the ingredient
  public onChangeIngredient(id: any): void {
    const ingredient = this.listIngredient.find((x) => x.id === id);
    if (ingredient && !this.formGroup.value.purchaseUomId) {
      this.formGroup.patchValue({ purchaseUomId: ingredient.purchaseUomId || ingredient.baseUnitId || null });
    }
  }

  protected override validate(v: any): string | null {
    if (checkValidateBetweenTwoDate(v.effectiveFrom, v.effectiveTo)) {
      return 'Ngày hiệu lực đến phải sau ngày hiệu lực từ';
    }
    return null;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      supplierMaterialCode: (v.supplierMaterialCode ?? '').trim(),
      unitPrice: toNumberParam(v.unitPrice),
      minimumOrderQuantity: toNumberParam(v.minimumOrderQuantity),
      leadTimeDays: toNumberParam(v.leadTimeDays),
      effectiveFrom: toDateParam(v.effectiveFrom),
      effectiveTo: toDateParam(v.effectiveTo),
      isPreferred: !!v.isPreferred,
    };
  }
}
