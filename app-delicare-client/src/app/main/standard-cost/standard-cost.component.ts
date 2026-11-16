import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { toFormData } from 'src/app/helpers/common-helper';
import { COST_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { StandardCostService } from 'src/app/services/standard-cost.service';
import { MasterListBase } from 'src/app/shared/master/master-list.base';

// cost-standard – Giá thành chuẩn theo công thức (FE-3). StandardCost/GetAllData + tên món / mã phiên bản công thức.
// Bản ghi do Recipe/Lock và StandardCost/Recalculate sinh ra (vật tư + nhân công + chi phí chung / suất theo BusinessRules),
// nên màn hình chỉ xem + "Tính lại" (quyền Sửa trên #/cost-standard); không thêm / sửa / xóa tay.
// Status Active / Inactive (PROPOSED – BA/PO CONFIRM).
@Component({
  selector: 'app-standard-cost',
  templateUrl: './standard-cost.component.html',
})
export class StandardCostComponent extends MasterListBase {
  public readonly statuses = COST_STATUSES;
  public readonly labelOf = labelOf;
  public listDish: any[] = [];
  public recalcDishIds: any[] = [];
  public isRecalculating = false;
  protected searchFields = ['dishCode', 'dishName', 'versionCode'];
  protected override filterDefaults = { dishId: null, status: 'Active' };

  constructor(
    private standardCostService: StandardCostService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.standardCostService.getAllData(), this.lookup.dishes(), this.lookup.list('Recipe', (x) => x.versionCode ?? '')]).pipe(
      map(([res, dishes, recipes]) => {
        this.listDish = dishes;
        const dishById = new Map(dishes.map((d) => [String(d.id).toLowerCase(), d]));
        const recipeById = new Map(recipes.map((r) => [String(r.id).toLowerCase(), r]));
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => {
          const dish = dishById.get(String(x.dishId).toLowerCase());
          const recipe = recipeById.get(String(x.recipeId).toLowerCase());
          return {
            ...x,
            dishCode: dish?.code ?? '',
            dishName: dish?.name ?? '',
            versionCode: recipe?.versionCode || (recipe ? 'V' + recipe.versionNo : ''),
          };
        });
      })
    );
  }

  protected deleteRow(id: any) {
    return this.standardCostService.delete(id);
  }

  // StandardCost/Recalculate: listDishId = chosen dishes ('' = every dish with a locked recipe)
  public async recalculate(): Promise<void> {
    const ids = (this.recalcDishIds ?? []).filter((x) => !!x);
    const message = ids.length
      ? `Tính lại giá thành chuẩn cho ${ids.length} món đã chọn theo giá NCC hiện hành?`
      : 'Tính lại giá thành chuẩn cho TẤT CẢ món có công thức đã khóa theo giá NCC hiện hành?';
    if (!(await this.notify.confirm(message, 'Tính lại'))) {
      return;
    }
    this.isRecalculating = true;
    this.standardCostService.recalculate(toFormData({ listDishId: ids.join(',') })).subscribe({
      next: (res) => {
        this.isRecalculating = false;
        if (res?.responseCode === 200) {
          this.notify.success(`Đã tính lại giá thành chuẩn cho ${res.data ?? 0} món.`);
          this.reload();
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: () => {
        this.isRecalculating = false;
        this.notify.error();
      },
    });
  }
}
