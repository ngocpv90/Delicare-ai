import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { InventoryPolicyService } from 'src/app/services/inventory-policy.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { InventoryPolicyModalComponent } from './inventory-policy-modal/inventory-policy-modal.component';

// scm-inv-policy – Min / Max / Safety / Reorder theo nguyên liệu × kho (FE-2).
// Backend: Min ≤ Safety ≤ Reorder ≤ Max; một dòng cho (nguyên liệu, kho) – lỗi 400 hiện nguyên văn.
@Component({
  selector: 'app-inventory-policy',
  templateUrl: './inventory-policy.component.html',
})
export class InventoryPolicyComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public listIngredient: any[] = [];
  public listWarehouse: any[] = [];
  protected searchFields = ['ingredientName', 'warehouseName'];
  protected override filterDefaults = { ingredientId: null, warehouseId: null, status: '' };

  constructor(
    private inventoryPolicyService: InventoryPolicyService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([
      this.inventoryPolicyService.getAllData(),
      this.lookup.ingredients(),
      this.lookup.warehouses(),
      this.lookup.units(),
    ]).pipe(
      map(([res, ingredients, warehouses, units]) => {
        this.listIngredient = ingredients;
        this.listWarehouse = warehouses;
        const ingredientNames = labelMap(ingredients);
        const warehouseNames = labelMap(warehouses);
        const unitNames = labelMap(units, (x) => x.symbol || x.name);
        const ingredientUnit = new Map(ingredients.map((x) => [String(x.id).toLowerCase(), labelFrom(unitNames, x.baseUnitId)]));
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          ingredientName: labelFrom(ingredientNames, x.ingredientId),
          warehouseName: labelFrom(warehouseNames, x.warehouseId),
          unitName: ingredientUnit.get(String(x.ingredientId).toLowerCase()) ?? '',
          minimumStock: x.minimumStock ?? x.minStock,
          maximumStock: x.maximumStock ?? x.maxStock,
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.inventoryPolicyService.delete(id);
  }

  public openModal(data?: any): void {
    const { ingredientId, warehouseId } = this.searchForm.value;
    this.openForm(InventoryPolicyModalComponent, data ?? { ingredientId, warehouseId });
  }
}
