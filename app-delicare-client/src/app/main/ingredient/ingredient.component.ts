import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, labelOf } from 'src/app/helpers/master-data-options';
import { IngredientService } from 'src/app/services/ingredient.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { IngredientModalComponent } from './ingredient-modal/ingredient-modal.component';

// scm-ingredient – Danh mục nguyên liệu (FE-2) + ngăn kéo: thông tin, tồn khả dụng (Ingredient/GetStock).
@Component({
  selector: 'app-ingredient',
  templateUrl: './ingredient.component.html',
})
export class IngredientComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public storageTypes: CodeLabel[] = [];
  public listCategory: any[] = [];
  protected searchFields = ['code', 'name', 'specification', 'categoryName'];
  protected override filterDefaults = { ingredientCategoryId: null, storageType: null, status: '' };

  // Drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'info';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'info', label: 'Thông tin' },
    { key: 'stock', label: 'Tồn kho' },
  ];
  public stock: any;
  public isLoadingStock = false;
  private unitNames = new Map<string, string>();
  private warehouseNames = new Map<string, string>();

  constructor(
    private ingredientService: IngredientService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    this.metaDataService.getReferenceList('StorageType').subscribe((list) => (this.storageTypes = list));
  }

  protected loadRows() {
    return forkJoin([
      this.ingredientService.getAllData(),
      this.lookup.ingredientCategories(),
      this.lookup.units(),
      this.lookup.warehouses(),
    ]).pipe(
      map(([res, categories, units, warehouses]) => {
        this.listCategory = categories;
        const categoryNames = labelMap(categories);
        this.unitNames = labelMap(units, (x) => x.label);
        this.warehouseNames = labelMap(warehouses, (x) => x.label);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          categoryName: labelFrom(categoryNames, x.ingredientCategoryId),
          baseUnitName: labelFrom(this.unitNames, x.baseUnitId),
          purchaseUomName: labelFrom(this.unitNames, x.purchaseUomId),
          issueUomName: labelFrom(this.unitNames, x.issueUomId),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.ingredientService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(IngredientModalComponent, data, 'xl');
  }

  public warehouseName(id: any): string {
    return labelFrom(this.warehouseNames, id) || '(kho đã xóa)';
  }

  public openDrawer(row: any, tab = 'info'): void {
    this.drawerRow = row;
    this.drawerTab = tab;
    this.drawerOpen = true;
    this.stock = null;
    this.isLoadingStock = true;
    this.ingredientService.getStock(row.id).subscribe({
      next: (res) => {
        this.isLoadingStock = false;
        if (res?.responseCode === 200) {
          this.stock = res.data;
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: () => {
        this.isLoadingStock = false;
        this.notify.error();
      },
    });
  }

  public editFromDrawer(): void {
    this.drawerOpen = false;
    this.openModal(this.drawerRow);
  }
}
