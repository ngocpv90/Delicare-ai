import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ACTIVE_STATUSES, toNumberParam } from 'src/app/helpers/master-data-options';
import { InventoryPolicyService } from 'src/app/services/inventory-policy.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// InventoryPolicyRequest: IngredientId, WarehouseId, MinimumStock, MaximumStock, SafetyStock, UseFefo,
// ExpiryWarningDays, Status, MinStock, MaxStock, ReorderPoint, CriticalLevel, LeadTimeDays.
// MinStock / MaxStock are duplicates of MinimumStock / MaximumStock: written with the same value.
@Component({
  selector: 'app-inventory-policy-modal',
  templateUrl: './inventory-policy-modal.component.html',
})
export class InventoryPolicyModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listIngredient: any[] = [];
  public listWarehouse: any[] = [];
  protected requestFields = [
    'ingredientId', 'warehouseId', 'minimumStock', 'maximumStock', 'safetyStock', 'useFefo', 'expiryWarningDays',
    'status', 'minStock', 'maxStock', 'reorderPoint', 'criticalLevel', 'leadTimeDays',
  ];

  constructor(
    protected api: InventoryPolicyService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      ingredientId: [null, [Validators.required]],
      warehouseId: [null],
      minimumStock: [null, [Validators.min(0)]],
      safetyStock: [null, [Validators.min(0)]],
      reorderPoint: [null, [Validators.min(0)]],
      maximumStock: [null, [Validators.min(0)]],
      criticalLevel: [null, [Validators.min(0)]],
      leadTimeDays: [null, [Validators.min(0)]],
      useFefo: [true],
      expiryWarningDays: [null, [Validators.min(0)]],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    forkJoin([this.lookup.ingredients(), this.lookup.warehouses()]).subscribe(([ingredients, warehouses]) => {
      this.listIngredient = ingredients;
      this.listWarehouse = warehouses;
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue({
      ...record,
      minimumStock: record.minimumStock ?? record.minStock,
      maximumStock: record.maximumStock ?? record.maxStock,
    });
  }

  // Same rule as the backend (spec 4.6): Tối thiểu ≤ An toàn ≤ Đặt lại ≤ Tối đa (empty values skipped)
  protected override validate(v: any): string | null {
    const chain = [v.minimumStock, v.safetyStock, v.reorderPoint, v.maximumStock].filter((x) => x !== null && x !== undefined);
    for (let i = 1; i < chain.length; i++) {
      if (Number(chain[i - 1]) > Number(chain[i])) {
        return 'Phải thỏa: Tối thiểu ≤ An toàn ≤ Điểm đặt lại ≤ Tối đa';
      }
    }
    return null;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      minimumStock: toNumberParam(v.minimumStock),
      minStock: toNumberParam(v.minimumStock),
      maximumStock: toNumberParam(v.maximumStock),
      maxStock: toNumberParam(v.maximumStock),
      safetyStock: toNumberParam(v.safetyStock),
      reorderPoint: toNumberParam(v.reorderPoint),
      criticalLevel: toNumberParam(v.criticalLevel),
      leadTimeDays: toNumberParam(v.leadTimeDays),
      expiryWarningDays: toNumberParam(v.expiryWarningDays),
      useFefo: !!v.useFefo,
    };
  }
}
