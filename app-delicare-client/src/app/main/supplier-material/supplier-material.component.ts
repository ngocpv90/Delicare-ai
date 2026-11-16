import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { toDateParam } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { SupplierMaterialService } from 'src/app/services/supplier-material.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { SupplierMaterialModalComponent } from './supplier-material-modal/supplier-material-modal.component';

// scm-supplier-material – Bảng giá & MOQ theo NCC (FE-2). Lọc "hiệu lực tại ngày" như SupplierMaterial/GetAll
// ?EffectiveOn= (EffectiveFrom ≤ ngày ≤ EffectiveTo, đầu mút trống = không giới hạn). Mỗi nguyên liệu chỉ 1 NCC
// ưu tiên: backend tự bỏ cờ ưu tiên của các dòng khác cùng nguyên liệu khi lưu.
@Component({
  selector: 'app-supplier-material',
  templateUrl: './supplier-material.component.html',
})
export class SupplierMaterialComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public listSupplier: any[] = [];
  public listIngredient: any[] = [];
  protected searchFields = ['supplierMaterialCode', 'supplierName', 'ingredientName', 'qualityRequirement'];
  protected override filterDefaults = { supplierId: null, ingredientId: null, effectiveOn: null, preferredOnly: false, status: '' };
  protected override customFilters = ['effectiveOn', 'preferredOnly'];

  constructor(
    private supplierMaterialService: SupplierMaterialService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([
      this.supplierMaterialService.getAllData(),
      this.lookup.suppliers(),
      this.lookup.ingredients(),
      this.lookup.units(),
    ]).pipe(
      map(([res, suppliers, ingredients, units]) => {
        this.listSupplier = suppliers;
        this.listIngredient = ingredients;
        const supplierNames = labelMap(suppliers);
        const ingredientNames = labelMap(ingredients);
        const unitNames = labelMap(units, (x) => x.symbol || x.name);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          supplierName: labelFrom(supplierNames, x.supplierId),
          ingredientName: labelFrom(ingredientNames, x.ingredientId),
          purchaseUomName: labelFrom(unitNames, x.purchaseUomId),
        }));
      })
    );
  }

  protected override matches(row: any, filter: any): boolean {
    if (filter.preferredOnly && !row.isPreferred) {
      return false;
    }
    const on = toDateParam(filter.effectiveOn);
    if (on) {
      const from = toDateParam(row.effectiveFrom);
      const to = toDateParam(row.effectiveTo);
      if ((from && from > on) || (to && to < on)) {
        return false;
      }
    }
    return true;
  }

  public effectiveToday(): void {
    this.setFilter('effectiveOn', new Date());
  }

  protected deleteRow(id: any) {
    return this.supplierMaterialService.delete(id);
  }

  public openModal(data?: any): void {
    const { supplierId, ingredientId } = this.searchForm.value;
    this.openForm(SupplierMaterialModalComponent, data ?? { supplierId, ingredientId });
  }
}
