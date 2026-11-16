import { Component, Input, OnInit } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { toFormData } from 'src/app/helpers/common-helper';
import { STRING_EMPTY } from 'src/app/helpers/constant-helper';
import { CompanyService } from 'src/app/services/company.service';
import { PositionService } from 'src/app/services/position.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { RECORD_STATUSES } from '../../organization-unit/org-options';

// adm-position create / edit. Sends every PositionRequest field (Update overwrites):
// Id, CompanyId, Code, Name, Description, Status, Note.
@Component({
  selector: 'app-position-modal',
  templateUrl: './position-modal.component.html',
})
export class PositionModalComponent implements OnInit {
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public formGroup!: UntypedFormGroup;
  public isLoading = false;
  public isLoadingCompany = false;
  public submitted = false;
  public listCompany: any[] = [];
  public readonly statuses = RECORD_STATUSES;

  constructor(
    private formBuilder: UntypedFormBuilder,
    private positionService: PositionService,
    private companyService: CompanyService,
    private notify: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.formGroup = this.formBuilder.group({
      id: [STRING_EMPTY],
      companyId: [null],
      code: [STRING_EMPTY, [Validators.required]],
      name: [STRING_EMPTY, [Validators.required]],
      description: [STRING_EMPTY],
      status: ['Active', [Validators.required]],
      note: [STRING_EMPTY],
    });
    this.getCompanies();
    if (this.data) {
      this.isLoading = true;
      this.positionService.getById(this.data.id).subscribe(
        (data) => {
          if (data.responseCode === 200) {
            this.formGroup.patchValue(data.data);
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

  private getCompanies(): void {
    this.isLoadingCompany = true;
    this.companyService.getAllData().subscribe(
      (data) => {
        if (data.responseCode === 200) {
          this.listCompany = data.listData ?? [];
          if (!this.data && this.listCompany.length === 1) {
            this.formGroup.patchValue({ companyId: this.listCompany[0].id });
          }
        }
      },
      () => {
        this.isLoadingCompany = false;
        this.notify.error();
      },
      () => {
        this.isLoadingCompany = false;
      }
    );
  }

  public isInvalid(name: string): boolean {
    const control = this.formGroup.controls[name];
    return control.invalid && (this.submitted || control.dirty || control.touched);
  }

  public destroyModal(reLoad: boolean): void {
    this.activeModal.close({ reLoad });
  }

  public submitForm(): void {
    this.submitted = true;
    if (!this.formGroup.valid) {
      return;
    }
    const v = this.formGroup.value;
    const params = {
      id: v.id || STRING_EMPTY,
      companyId: v.companyId ?? STRING_EMPTY,
      code: (v.code ?? STRING_EMPTY).trim(),
      name: (v.name ?? STRING_EMPTY).trim(),
      description: v.description ?? STRING_EMPTY,
      status: v.status ?? STRING_EMPTY,
      note: v.note ?? STRING_EMPTY,
    };
    this.isLoading = true;
    const formData = toFormData(params);
    const request = params.id ? this.positionService.update(formData) : this.positionService.insert(formData);
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
