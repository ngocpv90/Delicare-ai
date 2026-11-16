import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Observable, forkJoin, map } from 'rxjs';
import { menuUrl, permission, toDateParam, toFormData } from 'src/app/helpers/common-helper';
import { CodeLabel, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { RecipeLineService } from 'src/app/services/recipe-line.service';
import { RecipeService } from 'src/app/services/recipe.service';
import { DrawerTab } from 'src/app/shared/detail-drawer/detail-drawer.component';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { RecipeLineModalComponent } from './recipe-line-modal/recipe-line-modal.component';
import { RecipeModalComponent } from './recipe-modal/recipe-modal.component';
import { RecipeVersionModalComponent } from './recipe-version-modal/recipe-version-modal.component';

// menu-recipe – Công thức & phiên bản (FE-3). Mỗi phiên bản = 1 Recipe cùng DishId (không có bảng RecipeVersion).
// Danh sách: Recipe/GetAllData + tên món. Ngăn kéo: định lượng (RecipeLine CRUD khi Draft, quyền theo menu #/menu-recipe-line –
// ScreenMap RecipeLine → menu-recipe-line), giá vốn (Recipe/CalculateCost), các phiên bản của món (Recipe/GetByDishId), thông tin.
// Luồng: CreateVersion (Draft) → Submit (PendingApproval) → Approve (Approved) / Reject (→ Draft, bắt buộc lý do) → Lock (Locked,
// bản cũ → Obsolete, giá thành chuẩn mới). Duyệt / từ chối / khóa: quyền Approve = AllowEdit trên #/wf-inbox?type=menu-recipe
// (D-31, backend [Permission("menu-recipe", Approve)]); admin có mọi quyền. Trạng thái: Recipe.Status (STATUS_CATALOG, PROPOSED).
// Mở với ?dishId=<id> (từ menu-dish) để lọc sẵn theo món.
@Component({
  selector: 'app-recipe',
  templateUrl: './recipe.component.html',
})
export class RecipeComponent extends MasterListBase {
  public readonly labelOf = labelOf;
  public statuses: CodeLabel[] = [];
  public listDish: any[] = [];
  public linePermission: any;
  public canApprove = false;
  public isAdmin = false;
  protected searchFields = ['versionCode', 'dishCode', 'dishName', 'note'];
  protected override filterDefaults = { dishId: null, status: '' };

  // Drawer
  public drawerOpen = false;
  public drawerRow: any;
  public drawerTab = 'lines';
  public readonly drawerTabs: DrawerTab[] = [
    { key: 'lines', label: 'Định lượng' },
    { key: 'cost', label: 'Giá vốn NVL' },
    { key: 'versions', label: 'Các phiên bản' },
    { key: 'info', label: 'Thông tin' },
  ];
  public versions: any[] = [];
  public current: any = null; // RecipeVersionModel of drawerRow
  public isLoadingDrawer = false;
  public costMealCount = 100;
  public costOn: any = null;
  public cost: any = null;
  public isLoadingCost = false;
  private ingredientNames = new Map<string, string>();
  private unitNames = new Map<string, string>();
  private route = inject(ActivatedRoute);

  constructor(
    private recipeService: RecipeService,
    private recipeLineService: RecipeLineService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  protected override init(): void {
    const user = this.authenticationService.userValue;
    const listMenu = user?.listMenu;
    this.isAdmin = !!user?.isAdmin;
    this.linePermission = permission(menuUrl('#/menu-recipe-line'), listMenu);
    this.canApprove = this.isAdmin || !!permission(menuUrl('#/wf-inbox?type=menu-recipe'), listMenu)?.allowEdit;
    this.metaDataService.getStatusSet('Recipe.Status').subscribe((list) => (this.statuses = list));
    const dishId = this.route.snapshot.queryParamMap.get('dishId');
    if (dishId) {
      this.searchForm.patchValue({ dishId });
    }
    forkJoin([this.lookup.ingredients(), this.lookup.units()]).subscribe(([ingredients, units]) => {
      this.ingredientNames = labelMap(ingredients);
      this.unitNames = labelMap(units, (x) => x.symbol || x.name || '');
    });
  }

  public can(action: 'allowAdd' | 'allowEdit' | 'allowDelete'): boolean {
    return this.isAdmin || !!this.linePermission?.[action];
  }

  protected loadRows() {
    return forkJoin([this.recipeService.getAllData(), this.lookup.dishes()]).pipe(
      map(([res, dishes]) => {
        this.listDish = dishes;
        const byId = new Map(dishes.map((d) => [String(d.id).toLowerCase(), d]));
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => {
          const dish = byId.get(String(x.dishId).toLowerCase());
          return { ...x, dishCode: dish?.code ?? '', dishName: dish?.name ?? '' };
        });
      })
    );
  }

  protected deleteRow(id: any) {
    return this.recipeService.delete(id);
  }

  protected override afterSave(): void {
    this.reload();
    if (this.drawerOpen) {
      this.loadDrawer();
    }
  }

  // ---------------------------------------------------------------- versions
  public openCreateVersion(data?: any): void {
    this.modalService
      .openForResult(RecipeVersionModalComponent, {
        title: 'Tạo phiên bản công thức',
        data: data ?? { dishId: this.searchForm.value.dishId },
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.reload();
          if (result.recipe?.id) {
            this.openDrawer({ ...result.recipe, ...this.dishInfo(result.recipe.dishId) });
          }
        }
      });
  }

  public newVersionFrom(row: any): void {
    this.openCreateVersion({ dishId: row.dishId, copyFromRecipeId: row.id });
  }

  public openEdit(row: any): void {
    this.openForm(RecipeModalComponent, row, 'lg', true);
  }

  private dishInfo(dishId: any): any {
    const dish = this.listDish.find((d) => String(d.id).toLowerCase() === String(dishId).toLowerCase());
    return { dishCode: dish?.code ?? '', dishName: dish?.name ?? '' };
  }

  // ---------------------------------------------------------------- drawer
  public openDrawer(row: any): void {
    this.drawerRow = row;
    this.drawerTab = 'lines';
    this.drawerOpen = true;
    this.cost = null;
    this.costOn = null;
    this.costMealCount = Number(row.standardOutputQty) || 100;
    this.loadDrawer();
  }

  // Versions of the dish (GetByDishId) -> current version + lines + material cost per meal
  private loadDrawer(): void {
    this.isLoadingDrawer = true;
    this.recipeService.getByDishId(this.drawerRow.dishId).subscribe({
      next: (res) => {
        this.isLoadingDrawer = false;
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        this.versions = list.map((v) => ({
          ...v,
          listLine: (v.listLine ?? []).map((l: any) => ({
            ...l,
            ingredientName: labelFrom(this.ingredientNames, l.ingredientId),
            unitName: labelFrom(this.unitNames, l.unitId),
          })),
        }));
        this.current = this.versions.find((v) => String(v.recipe?.id).toLowerCase() === String(this.drawerRow.id).toLowerCase()) ?? null;
        if (this.current?.recipe) {
          this.drawerRow = { ...this.drawerRow, ...this.current.recipe };
        }
      },
      error: () => {
        this.isLoadingDrawer = false;
        this.notify.error();
      },
    });
  }

  public get isDraft(): boolean {
    return this.drawerRow?.status === 'Draft';
  }

  public selectVersion(v: any): void {
    this.openDrawer({ ...v.recipe, ...this.dishInfo(v.recipe.dishId) });
  }

  // ---------------------------------------------------------------- lines
  public openLineModal(line?: any): void {
    this.modalService
      .openForResult(RecipeLineModalComponent, {
        title: line ? 'Cập nhật định lượng' : 'Thêm nguyên liệu',
        data: line
          ? { ...line, standardOutputQty: this.drawerRow.standardOutputQty }
          : { recipeId: this.drawerRow.id, standardOutputQty: this.drawerRow.standardOutputQty },
      })
      .subscribe((result) => {
        if (result?.reLoad) {
          this.cost = null;
          this.loadDrawer();
        }
      });
  }

  public async deleteLine(id: any): Promise<void> {
    if (!(await this.notify.confirmDelete())) {
      return;
    }
    this.run(this.recipeLineService.delete(id), () => {
      this.cost = null;
      this.loadDrawer();
    });
  }

  // ---------------------------------------------------------------- workflow
  public async submit(): Promise<void> {
    if (!this.current?.listLine?.length) {
      this.notify.error('Công thức cần ít nhất 1 dòng định lượng trước khi gửi duyệt.');
      return;
    }
    if (!(await this.notify.confirm('Gửi phiên bản này đi duyệt? Sau khi gửi không sửa được định lượng.', 'Gửi duyệt'))) {
      return;
    }
    this.run(this.recipeService.submit(toFormData({ id: this.drawerRow.id })), () => this.afterSave());
  }

  public async approve(): Promise<void> {
    const note = await this.notify.prompt({ title: 'Duyệt công thức', label: 'Ghi chú (không bắt buộc)', confirmText: 'Duyệt' });
    if (note === null) {
      return;
    }
    this.run(this.recipeService.approve(toFormData({ id: this.drawerRow.id, note })), () => this.afterSave());
  }

  public async reject(): Promise<void> {
    const note = await this.notify.prompt({ title: 'Từ chối công thức', label: 'Lý do từ chối', required: true, confirmText: 'Từ chối' });
    if (note === null) {
      return;
    }
    this.run(this.recipeService.reject(toFormData({ id: this.drawerRow.id, note: note.trim() })), () => this.afterSave());
  }

  public async lock(): Promise<void> {
    const date = await this.notify.prompt({
      title: 'Khóa & áp dụng công thức',
      label: 'Áp dụng từ ngày (bản đang áp dụng sẽ hết hiệu lực từ ngày này; giá thành chuẩn được tính lại)',
      input: 'date',
      value: toDateParam(new Date()),
      required: true,
      confirmText: 'Khóa',
    });
    if (date === null) {
      return;
    }
    this.run(this.recipeService.lock(toFormData({ id: this.drawerRow.id, effectiveFrom: date })), () => this.afterSave());
  }

  public async deleteVersion(): Promise<void> {
    if (!(await this.notify.confirmDelete('Xóa phiên bản nháp này? Cần xóa hết dòng định lượng trước.'))) {
      return;
    }
    this.run(this.recipeService.delete(this.drawerRow.id), () => {
      this.drawerOpen = false;
      this.reload();
    });
  }

  // ---------------------------------------------------------------- cost
  public calculateCost(): void {
    const mealCount = Number(this.costMealCount);
    if (!(mealCount > 0)) {
      this.notify.error('Số suất phải lớn hơn 0');
      return;
    }
    this.isLoadingCost = true;
    this.recipeService.calculateCost(this.drawerRow.id, mealCount, toDateParam(this.costOn)).subscribe({
      next: (res) => {
        this.isLoadingCost = false;
        if (res?.responseCode === 200 && res.data) {
          this.cost = {
            ...res.data,
            listLine: (res.data.listLine ?? []).map((l: any) => ({
              ...l,
              ingredientName: l.ingredientName || labelFrom(this.ingredientNames, l.ingredientId),
              unitName: labelFrom(this.unitNames, l.unitId),
              priceUnitName: labelFrom(this.unitNames, l.priceUnitId),
            })),
          };
        } else {
          this.cost = null;
          this.notify.error(res?.responseMessage);
        }
      },
      error: () => {
        this.isLoadingCost = false;
        this.notify.error();
      },
    });
  }

  // Business action: responseCode 200 -> success toast + done(); else responseMessage
  private run(request: Observable<any>, done: () => void): void {
    this.isLoading = true;
    request.subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res?.responseCode === 200) {
          this.notify.success(res.responseMessage);
          done();
        } else {
          this.notify.error(res?.responseMessage);
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.notify.error(err?.status === 403 ? 'Bạn không có quyền thực hiện thao tác này' : undefined);
      },
    });
  }
}
