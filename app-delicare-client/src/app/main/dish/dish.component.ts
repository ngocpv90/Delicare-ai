import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { menuUrl } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { DishService } from 'src/app/services/dish.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { DishModalComponent } from './dish-modal/dish-modal.component';

// menu-dish – Danh mục món ăn (FE-3). Dish/GetAllData + ngăn kéo: thông tin món, công thức đang hiệu lực
// (Dish/GetActiveRecipe: phiên bản đã khóa, dòng định lượng, giá vốn NVL / suất). Nút "Nhập Excel" → sys-import?table=Dish.
@Component({
  selector: 'app-dish',
  templateUrl: './dish.component.html',
})
export class DishComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public listCategory: any[] = [];
  protected searchFields = ['code', 'name', 'categoryName', 'mealCategory', 'description'];
  protected override filterDefaults = { status: '', dishCategoryId: null };

  // Drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'recipe';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'recipe', label: 'Công thức đang áp dụng' },
    { key: 'info', label: 'Thông tin món' },
  ];
  public activeRecipe: any = null;
  public activeRecipeMessage = '';
  public isLoadingRecipe = false;
  private ingredientNames = new Map<string, string>();
  private unitNames = new Map<string, string>();

  constructor(
    private dishService: DishService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.dishService.getAllData(), this.lookup.dishCategories(), this.lookup.units()]).pipe(
      map(([res, categories, units]) => {
        this.listCategory = categories;
        const categoryNames = labelMap(categories, (x) => x.name ?? '');
        this.unitNames = labelMap(units, (x) => x.symbol || x.name || '');
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => {
          const dishCategoryId = x.dishCategoryId ?? x.categoryId;
          return {
            ...x,
            dishCategoryId,
            categoryName: labelFrom(categoryNames, dishCategoryId),
            unitName: labelFrom(this.unitNames, x.defaultUnitId),
          };
        });
      })
    );
  }

  protected deleteRow(id: any) {
    return this.dishService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(DishModalComponent, data);
  }

  public openImport(): void {
    this.router.navigate([menuUrl('#/sys-import')], { queryParams: { table: 'Dish' } });
  }

  public openRecipes(row: any): void {
    this.router.navigate([menuUrl('#/menu-recipe')], { queryParams: { dishId: row.id } });
  }

  public openDrawer(row: any): void {
    this.drawerRow = row;
    this.drawerTab = 'recipe';
    this.drawerOpen = true;
    this.loadActiveRecipe();
  }

  public editFromDrawer(): void {
    this.drawerOpen = false;
    this.openModal(this.drawerRow);
  }

  private loadActiveRecipe(): void {
    this.activeRecipe = null;
    this.activeRecipeMessage = '';
    this.isLoadingRecipe = true;
    const ingredients$ = this.ingredientNames.size ? null : this.lookup.ingredients();
    forkJoin([this.dishService.getActiveRecipe(this.drawerRow.id), ...(ingredients$ ? [ingredients$] : [])]).subscribe({
      next: ([res, ingredients]) => {
        this.isLoadingRecipe = false;
        if (ingredients) {
          this.ingredientNames = labelMap(ingredients);
        }
        if (res?.responseCode === 200 && res.data) {
          this.activeRecipe = {
            ...res.data,
            listLine: (res.data.listLine ?? []).map((l: any) => ({
              ...l,
              ingredientName: labelFrom(this.ingredientNames, l.ingredientId),
              unitName: labelFrom(this.unitNames, l.unitId),
            })),
          };
        } else {
          // 400 "không tìm thấy" = the dish has no locked recipe yet
          this.activeRecipeMessage = 'Món chưa có công thức đã khóa (đang áp dụng).';
        }
      },
      error: () => {
        this.isLoadingRecipe = false;
        this.notify.error();
      },
    });
  }
}
