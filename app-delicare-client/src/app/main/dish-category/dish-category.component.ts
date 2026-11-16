import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { DishCategoryService } from 'src/app/services/dish-category.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { DishCategoryModalComponent } from './dish-category-modal/dish-category-modal.component';

// menu-dish-cat – Nhóm món ăn (FE-3). DishCategory/GetAllData; nhóm cha = ParentId; số món theo Dish.DishCategoryId
// (cột cũ Dish.CategoryId khi DishCategoryId trống). Status: Active / Inactive (PROPOSED – BA/PO CONFIRM).
@Component({
  selector: 'app-dish-category',
  templateUrl: './dish-category.component.html',
})
export class DishCategoryComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  protected searchFields = ['code', 'name', 'description'];
  protected override filterDefaults = { status: '' };

  constructor(
    private dishCategoryService: DishCategoryService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.dishCategoryService.getAllData(), this.lookup.dishes()]).pipe(
      map(([res, dishes]) => {
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        const names = labelMap(list);
        const count = new Map<string, number>();
        dishes.forEach((x) => {
          const key = String(x.dishCategoryId ?? x.categoryId ?? '').toLowerCase();
          count.set(key, (count.get(key) ?? 0) + 1);
        });
        return list.map((x) => ({
          ...x,
          parentName: labelFrom(names, x.parentId),
          dishCount: count.get(String(x.id).toLowerCase()) ?? 0,
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.dishCategoryService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(DishCategoryModalComponent, data);
  }
}
