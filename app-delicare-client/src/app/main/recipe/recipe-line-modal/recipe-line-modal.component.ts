import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { toNumberParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { RecipeLineService } from 'src/app/services/recipe-line.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// RecipeLineRequest: RecipeId, IngredientId, Quantity, UnitId, ExpectedWasteRate, PreparationNote, SortOrder, WastePercent,
// YieldPercent, SequenceNo, Note. Backend: only while the recipe is Draft; quantity > 0; waste % in [0, 100);
// SortOrder 0 on insert = put last. Quantity is for Recipe.StandardOutputQty servings (default 100).
// WastePercent = % hao hụt sơ chế, ExpectedWasteRate = % hao hụt chế biến (same mapping as the Excel import, T49).
// Open with data = line (edit) or { recipeId, standardOutputQty } (create).
@Component({
  selector: 'app-recipe-line-modal',
  templateUrl: './recipe-line-modal.component.html',
})
export class RecipeLineModalComponent extends MasterModalBase {
  public listIngredient: any[] = [];
  public listUnit: any[] = [];
  protected requestFields = [
    'recipeId', 'ingredientId', 'quantity', 'unitId', 'expectedWasteRate', 'preparationNote', 'sortOrder', 'wastePercent',
    'yieldPercent', 'sequenceNo', 'note',
  ];

  constructor(
    protected api: RecipeLineService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      recipeId: [null, [Validators.required]],
      ingredientId: [null, [Validators.required]],
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unitId: [null, [Validators.required]],
      wastePercent: [null, [Validators.min(0), Validators.max(99.99)]],
      expectedWasteRate: [null, [Validators.min(0), Validators.max(99.99)]],
      preparationNote: [''],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    forkJoin([this.lookup.ingredients(), this.lookup.units()]).subscribe(([ingredients, units]) => {
      this.listIngredient = ingredients;
      this.listUnit = units;
      this.isLoadingList = false;
    });
  }

  // Default unit = base unit of the ingredient
  public ingredientChanged(id: any): void {
    const item = this.listIngredient.find((x) => x.id === id);
    if (item?.baseUnitId && !this.formGroup.value.unitId) {
      this.formGroup.patchValue({ unitId: item.baseUnitId });
    }
  }

  protected override toParams(v: any) {
    return {
      ...v,
      quantity: toNumberParam(v.quantity),
      wastePercent: toNumberParam(v.wastePercent),
      expectedWasteRate: toNumberParam(v.expectedWasteRate),
      // non-nullable int on the backend: 0 on insert = put last
      sortOrder: this.record?.sortOrder ?? 0,
      sequenceNo: this.record?.sequenceNo ?? '',
    };
  }
}
