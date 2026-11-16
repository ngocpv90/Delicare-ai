import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ACTIVE_STATUSES, DISH_MEAL_CATEGORY_SUGGESTIONS, toNumberParam } from 'src/app/helpers/master-data-options';
import { DishService } from 'src/app/services/dish.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// DishRequest: DishCategoryId, DefaultUnitId, Code, Name, Description, StandardServingSize, Status, Note, MealCategory,
// StandardPortion, StandardCost. StandardCost is written by Recipe/Lock and StandardCost/Recalculate – not edited here
// (copied from GetById). Decimals are sent with '.' (backend form-binding culture issue – see PROGRESS FE-3).
@Component({
  selector: 'app-dish-modal',
  templateUrl: './dish-modal.component.html',
})
export class DishModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly mealCategorySuggestions = DISH_MEAL_CATEGORY_SUGGESTIONS;
  public listCategory: any[] = [];
  public listUnit: any[] = [];
  protected requestFields = [
    'dishCategoryId', 'defaultUnitId', 'code', 'name', 'description', 'standardServingSize', 'status', 'note',
    'mealCategory', 'standardPortion', 'standardCost',
  ];

  constructor(
    protected api: DishService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  public get standardCost(): any {
    return this.record?.standardCost;
  }

  protected buildForm() {
    return this.formBuilder.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      dishCategoryId: [null],
      mealCategory: ['', [Validators.maxLength(100)]],
      defaultUnitId: [null],
      standardPortion: [null, [Validators.min(0)]],
      standardServingSize: [null, [Validators.min(0)]],
      status: ['Active', [Validators.required]],
      description: [''],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    forkJoin([this.lookup.dishCategories(), this.lookup.units()]).subscribe(([categories, units]) => {
      this.listCategory = categories;
      this.listUnit = units;
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    // legacy column Dish.CategoryId is shown when DishCategoryId is empty
    this.formGroup.patchValue({ ...record, dishCategoryId: record.dishCategoryId ?? record.categoryId });
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      mealCategory: (v.mealCategory ?? '').trim(),
      standardPortion: toNumberParam(v.standardPortion),
      standardServingSize: toNumberParam(v.standardServingSize),
      standardCost: toNumberParam(this.record?.standardCost),
    };
  }
}
