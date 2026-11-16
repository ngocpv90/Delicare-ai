import { Directive, Input, OnInit, inject } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup } from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Observable } from 'rxjs';
import { toFormData } from '../../helpers/common-helper';
import { STRING_EMPTY } from '../../helpers/constant-helper';
import { NotifyService } from '../notify.service';

export interface CrudApi {
  getById(id: any): Observable<any>;
  insert(params: any): Observable<any>;
  update(params: any): Observable<any>;
}

// Shared behaviour of the FE-2 create / edit modals (same flow as unit-measure-modal / employee-modal).
// Legacy Update overwrites every column (D-30), so the body always carries EVERY <Entity>Request field:
// edited values from the form, the rest copied from the record loaded by GetById (fields the form does
// not show are kept as they are).
// Open with data = row (edit, has id) or data = { …defaults } without id (create with preset values).
@Directive()
export abstract class MasterModalBase implements OnInit {
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public formGroup!: UntypedFormGroup;
  public isLoading = false;
  public isLoadingList = false;
  public submitted = false;
  protected record: any = {};

  protected formBuilder = inject(UntypedFormBuilder);
  protected notify = inject(NotifyService);
  protected activeModal = inject(NgbActiveModal);

  protected abstract api: CrudApi;
  // Every field of the backend <Entity>Request (camelCase, without id)
  protected abstract requestFields: string[];
  protected abstract buildForm(): UntypedFormGroup;

  // Form value -> request values (dates, times, numbers…); keys not returned are taken from the record
  protected toParams(value: any): Record<string, any> {
    return value;
  }

  // Extra checks before saving; return a message to block the save
  protected validate(_value: any): string | null {
    return null;
  }

  protected loadLists(): void {}

  protected patchRecord(record: any): void {
    this.formGroup.patchValue(record);
  }

  public get isEdit(): boolean {
    return !!this.data?.id;
  }

  public ngOnInit(): void {
    this.formGroup = this.buildForm();
    this.loadLists();
    if (this.isEdit) {
      this.isLoading = true;
      this.api.getById(this.data.id).subscribe({
        next: (res) => {
          this.isLoading = false;
          if (res?.responseCode === 200 && res.data) {
            this.record = res.data;
            this.patchRecord(res.data);
          } else {
            this.notify.error(res?.responseMessage);
          }
        },
        error: () => {
          this.isLoading = false;
          this.notify.error();
        },
      });
    } else if (this.data) {
      this.formGroup.patchValue(this.data);
    }
  }

  public isInvalid(name: string): boolean {
    const control = this.formGroup.controls[name];
    return !!control && control.invalid && (this.submitted || control.dirty || control.touched);
  }

  public destroyModal(reLoad: boolean): void {
    this.activeModal.close({ reLoad });
  }

  public submitForm(): void {
    this.submitted = true;
    this.formGroup.markAllAsTouched();
    if (!this.formGroup.valid) {
      return;
    }
    const value = this.formGroup.getRawValue();
    const message = this.validate(value);
    if (message) {
      this.notify.error(message);
      return;
    }
    const values = this.toParams(value);
    const params: Record<string, any> = { id: this.record?.id || (this.isEdit ? this.data.id : STRING_EMPTY) };
    this.requestFields.forEach((field) => {
      params[field] = field in values ? values[field] : this.record?.[field] ?? STRING_EMPTY;
    });
    this.isLoading = true;
    const formData = toFormData(params);
    const request = params['id'] ? this.api.update(formData) : this.api.insert(formData);
    request.subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res?.responseCode === 200) {
          this.notify.success(res.responseMessage);
          this.destroyModal(true);
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
