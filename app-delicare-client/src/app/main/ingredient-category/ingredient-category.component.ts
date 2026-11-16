import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { IngredientCategoryService } from 'src/app/services/ingredient-category.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { IngredientCategoryModalComponent } from './ingredient-category-modal/ingredient-category-modal.component';

// scm-ingredient-cat – Nhóm nguyên liệu (FE-2). IngredientCategory/GetAllData; nhóm cha = ParentId.
@Component({
  selector: 'app-ingredient-category',
  templateUrl: './ingredient-category.component.html',
})
export class IngredientCategoryComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  protected searchFields = ['code', 'name', 'description'];
  protected override filterDefaults = { status: '' };

  constructor(
    private ingredientCategoryService: IngredientCategoryService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.ingredientCategoryService.getAllData(), this.lookup.ingredients()]).pipe(
      map(([res, ingredients]) => {
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        const names = labelMap(list);
        const count = new Map<string, number>();
        ingredients.forEach((x) => {
          const key = String(x.ingredientCategoryId ?? '').toLowerCase();
          count.set(key, (count.get(key) ?? 0) + 1);
        });
        return list.map((x) => ({
          ...x,
          parentName: labelFrom(names, x.parentId),
          ingredientCount: count.get(String(x.id).toLowerCase()) ?? 0,
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.ingredientCategoryService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(IngredientCategoryModalComponent, data);
  }
}
