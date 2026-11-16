import { Component, Input, OnInit } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { toFormData } from 'src/app/helpers/common-helper';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { RecipeService } from 'src/app/services/recipe.service';
import { NotifyService } from 'src/app/shared/notify.service';

// Recipe/CreateVersion (form: DishId, CopyFromRecipeId) – new Draft version of a dish, optionally copying the lines of
// an older version of the same dish. Backend refuses when the dish already has a Draft / PendingApproval version.
// Open with data = { dishId?, copyFromRecipeId? }. Closes with { reLoad, recipe } (recipe = the new Draft).
@Component({
  selector: 'app-recipe-version-modal',
  templateUrl: './recipe-version-modal.component.html',
})
export class RecipeVersionModalComponent implements OnInit {
  @Input() public title = '';
  @Input() public data: any;
  public formGroup!: UntypedFormGroup;
  public listDish: any[] = [];
  public listVersion: any[] = [];
  public isLoading = false;
  public isLoadingList = false;
  public submitted = false;
  private statusLabels: Record<string, string> = {};

  constructor(
    private formBuilder: UntypedFormBuilder,
    private activeModal: NgbActiveModal,
    private notify: NotifyService,
    private lookup: MasterLookupService,
    private recipeService: RecipeService,
    private metaDataService: MetaDataService
  ) {}

  public ngOnInit(): void {
    this.formGroup = this.formBuilder.group({
      dishId: [this.data?.dishId ?? null, [Validators.required]],
      copyFromRecipeId: [this.data?.copyFromRecipeId ?? null],
    });
    this.metaDataService.getStatusSet('Recipe.Status').subscribe((list) => {
      list.forEach((x) => (this.statusLabels[x.code] = x.label));
    });
    this.isLoadingList = true;
    this.lookup.dishes().subscribe((list) => {
      this.listDish = list;
      this.isLoadingList = false;
    });
    if (this.data?.dishId) {
      this.loadVersions(this.data.dishId, false);
    }
  }

  public dishChanged(dishId: any): void {
    this.loadVersions(dishId, true);
  }

  private loadVersions(dishId: any, resetCopy: boolean): void {
    if (resetCopy) {
      this.formGroup.patchValue({ copyFromRecipeId: null });
    }
    this.listVersion = [];
    if (!dishId) {
      return;
    }
    this.recipeService.getByDishId(dishId).subscribe({
      next: (res) => {
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        this.listVersion = list
          .filter((x) => x.recipe)
          .map((x) => ({
            id: x.recipe.id,
            label: `${x.recipe.versionCode || 'V' + x.recipe.versionNo} · ${this.statusLabels[x.recipe.status] ?? x.recipe.status ?? ''} · ${x.listLine?.length ?? 0} dòng`,
          }));
        // default: copy from the newest version
        if (resetCopy && this.listVersion.length) {
          this.formGroup.patchValue({ copyFromRecipeId: this.listVersion[0].id });
        }
      },
      error: () => this.notify.error(),
    });
  }

  public isInvalid(name: string): boolean {
    const control = this.formGroup.controls[name];
    return !!control && control.invalid && (this.submitted || control.dirty || control.touched);
  }

  public destroyModal(reLoad: boolean, recipe?: any): void {
    this.activeModal.close({ reLoad, recipe });
  }

  public submitForm(): void {
    this.submitted = true;
    if (!this.formGroup.valid) {
      return;
    }
    const value = this.formGroup.value;
    this.isLoading = true;
    this.recipeService
      .createVersion(toFormData({ dishId: value.dishId, copyFromRecipeId: value.copyFromRecipeId ?? '' }))
      .subscribe({
        next: (res) => {
          this.isLoading = false;
          if (res?.responseCode === 200) {
            this.notify.success(res.responseMessage);
            this.destroyModal(true, res.data);
          } else {
            this.notify.error(res?.responseMessage);
          }
        },
        error: () => {
          this.isLoading = false;
          this.notify.error();
        },
      });
  }
}
