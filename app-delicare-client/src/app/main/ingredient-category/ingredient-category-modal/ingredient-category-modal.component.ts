import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES } from 'src/app/helpers/master-data-options';
import { IngredientCategoryService } from 'src/app/services/ingredient-category.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// IngredientCategoryRequest: ParentId, Code, Name, Description, Status, Note
@Component({
  selector: 'app-ingredient-category-modal',
  templateUrl: './ingredient-category-modal.component.html',
})
export class IngredientCategoryModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listParent: any[] = [];
  protected requestFields = ['parentId', 'code', 'name', 'description', 'status', 'note'];

  constructor(
    protected api: IngredientCategoryService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      parentId: [null],
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      description: [''],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.lookup.ingredientCategories().subscribe((list) => {
      // a category cannot be its own parent
      this.listParent = list.filter((x) => x.id !== this.data?.id);
      this.isLoadingList = false;
    });
  }

  protected override toParams(v: any) {
    return { ...v, code: (v.code ?? '').trim(), name: (v.name ?? '').trim() };
  }
}
