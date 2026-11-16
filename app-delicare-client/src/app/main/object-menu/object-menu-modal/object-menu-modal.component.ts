import { Component, Input, OnInit } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { toFormData } from 'src/app/helpers/common-helper';
import { PROMOTE, STRING_EMPTY } from 'src/app/helpers/constant-helper';
import { ObjectMenuService } from 'src/app/services/object-menu.service';
import { NotifyService } from 'src/app/shared/notify.service';

// adm-objectmenu create / edit. data = { id } (edit) or { parentId } (new, optionally under a group).
// Sends every ObjectMenuRequest field (Update overwrites): Id, Name, ShortName, ParentId, MetaLink, IconName,
// LevelShow, OrderShow, DataTarget, AriaControls, IsActive, IsDefault. Menu has 2 levels only.
@Component({
  selector: 'app-object-menu-modal',
  templateUrl: './object-menu-modal.component.html',
})
export class ObjectMenuModalComponent implements OnInit {
  public isLoading = false;
  public isLoadingParentId = false;
  public formGroup!: UntypedFormGroup;
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public submitted = false;
  public listParent: any[] = [];

  constructor(
    private formBuilder: UntypedFormBuilder,
    private objectMenuService: ObjectMenuService,
    private notify: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.formGroup = this.formBuilder.group({
      id: [STRING_EMPTY],
      name: [STRING_EMPTY, [Validators.required]],
      shortName: [STRING_EMPTY],
      parentId: [this.data?.parentId ?? null],
      metaLink: [STRING_EMPTY, [Validators.required]],
      iconName: [STRING_EMPTY],
      levelShow: [this.data?.parentId ? 2 : 1, [Validators.required]],
      orderShow: [1, [Validators.required]],
      dataTarget: [STRING_EMPTY],
      ariaControls: [STRING_EMPTY],
      isActive: [true],
      isDefault: [false],
    });
    this.getAllData();
    if (this.data?.id) {
      this.isLoading = true;
      this.objectMenuService.getById(this.data.id).subscribe(
        (data) => {
          if (data.responseCode === 200) {
            const item = data.data;
            if (item.dataTarget) {
              item.dataTarget = item.dataTarget.replace(PROMOTE, STRING_EMPTY);
            }
            this.formGroup.patchValue(item);
          } else {
            this.notify.error(data.responseMessage);
          }
        },
        () => {
          this.isLoading = false;
          this.notify.error();
        },
        () => {
          this.isLoading = false;
        }
      );
    }
  }

  // Parents = level-1 menus (2-level menu), never the menu itself
  private getAllData(): void {
    this.isLoadingParentId = true;
    this.objectMenuService.getAllData().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.listParent = (data.listData ?? [])
            .filter((x: any) => (x.levelShow ?? 1) === 1 && x.id !== this.data?.id)
            .sort((a: any, b: any) => (a.orderShow ?? 0) - (b.orderShow ?? 0));
        }
      },
      () => {
        this.isLoadingParentId = false;
        this.notify.error();
      },
      () => {
        this.isLoadingParentId = false;
      }
    );
  }

  public destroyModal(reLoad: boolean): void {
    this.activeModal.close({ reLoad });
  }

  public isInvalid(name: string): boolean {
    const control = this.formGroup.controls[name];
    return control.invalid && (this.submitted || control.dirty || control.touched);
  }

  public onChangeParentId(value: any): void {
    this.formGroup.controls['levelShow'].setValue(value ? 2 : 1);
  }

  public onChangeDataTarget(value: any): void {
    this.formGroup.controls['ariaControls'].setValue(value || STRING_EMPTY);
  }

  public submitForm(): void {
    this.submitted = true;
    if (!this.formGroup.valid) {
      return;
    }
    const value = this.formGroup.value;
    const params = {
      id: value.id || STRING_EMPTY,
      name: (value.name ?? STRING_EMPTY).trim(),
      shortName: value.shortName ?? STRING_EMPTY,
      parentId: value.parentId ?? STRING_EMPTY,
      metaLink: (value.metaLink ?? STRING_EMPTY).trim(),
      iconName: value.iconName ?? STRING_EMPTY,
      levelShow: value.parentId ? 2 : 1,
      orderShow: value.orderShow ?? 1,
      dataTarget: value.dataTarget ? PROMOTE.concat(value.dataTarget) : STRING_EMPTY,
      ariaControls: value.ariaControls ?? STRING_EMPTY,
      isActive: !!value.isActive,
      isDefault: !!value.isDefault,
    };
    this.isLoading = true;
    const formData = toFormData(params);
    const request = params.id
      ? this.objectMenuService.update(formData)
      : this.objectMenuService.insert(formData);
    request.subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.notify.success(data.responseMessage);
          this.destroyModal(true);
        } else {
          this.notify.error(data.responseMessage);
        }
      },
      () => {
        this.isLoading = false;
        this.notify.error();
      },
      () => {
        this.isLoading = false;
      }
    );
  }
}
