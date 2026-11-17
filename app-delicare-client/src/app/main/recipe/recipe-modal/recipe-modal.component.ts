import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { toNumberParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { RecipeService } from 'src/app/services/recipe.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// Edit the header of a Draft recipe version (Recipe/Update – backend: only Draft; DishId, Status, IsActive, VersionNo,
// EffectiveFrom / EffectiveTo are kept by the service). New versions are created with Recipe/CreateVersion, not Insert.
// RecipeRequest.EffectiveFrom is a non-nullable DateTime: an empty value fails model binding (400), so EffectiveFrom /
// EffectiveTo / IsActive / Status are NOT sent (the service ignores them on Update anyway).
@Component({
  selector: 'app-recipe-modal',
  templateUrl: './recipe-modal.component.html',
})
export class RecipeModalComponent extends MasterModalBase {
  public listUnit: any[] = [];
  protected requestFields = [
    'dishId', 'versionCode', 'yieldQuantity', 'yieldUnitId', 'instructions', 'versionNo', 'yieldPercent', 'standardOutputQty', 'note',
  ];

  constructor(
    protected api: RecipeService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      versionCode: ['', [Validators.maxLength(50)]],
      standardOutputQty: [null, [Validators.required, Validators.min(1)]],
      yieldQuantity: [null, [Validators.required, Validators.min(0)]],
      yieldUnitId: [null],
      yieldPercent: [null, [Validators.min(0), Validators.max(100)]],
      instructions: [''],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.lookup.units().subscribe((units) => {
      this.listUnit = units;
      this.isLoadingList = false;
    });
  }

  protected override toParams(v: any) {
    return {
      ...v,
      versionCode: (v.versionCode ?? '').trim(),
      standardOutputQty: toNumberParam(v.standardOutputQty),
      yieldQuantity: toNumberParam(v.yieldQuantity),
      yieldPercent: toNumberParam(v.yieldPercent),
    };
  }
}
