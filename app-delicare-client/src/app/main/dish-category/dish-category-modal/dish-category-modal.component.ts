import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES } from 'src/app/helpers/master-data-options';
import { DishCategoryService } from 'src/app/services/dish-category.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// DishCategoryRequest: ParentId, Code, Name, Description, Status, Note
@Component({
  selector: 'app-dish-category-modal',
  templateUrl: './dish-category-modal.component.html',
})
export class DishCategoryModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listParent: any[] = [];
  protected requestFields = ['parentId', 'code', 'name', 'description', 'status', 'note'];

  constructor(
    protected api: DishCategoryService,
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
    this.lookup.dishCategories().subscribe((list) => {
      // a category cannot be its own parent
      this.listParent = list.filter((x) => x.id !== this.data?.id);
      this.isLoadingList = false;
    });
  }

  protected override toParams(v: any) {
    return { ...v, code: (v.code ?? '').trim(), name: (v.name ?? '').trim() };
  }
}
