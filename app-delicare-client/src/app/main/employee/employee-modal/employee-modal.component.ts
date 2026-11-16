import { Component, Input, OnInit } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin } from 'rxjs';
import { toDateParam, toFormData } from 'src/app/helpers/common-helper';
import { STRING_EMPTY } from 'src/app/helpers/constant-helper';
import { CompanyService } from 'src/app/services/company.service';
import { EmployeeService } from 'src/app/services/employee.service';
import { OrganizationUnitService } from 'src/app/services/organization-unit.service';
import { PositionService } from 'src/app/services/position.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { EMPLOYEE_STATUSES, GENDERS } from '../../organization-unit/org-options';

// adm-employee create / edit. Sends every EmployeeRequest field (Update overwrites): Id, CompanyId,
// OrganizationUnitId, PositionId, Code, FullName, DateOfBirth, Gender, Phone, Email, Address, HireDate, Status.
@Component({
  selector: 'app-employee-modal',
  templateUrl: './employee-modal.component.html',
})
export class EmployeeModalComponent implements OnInit {
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public formGroup!: UntypedFormGroup;
  public isLoading = false;
  public isLoadingList = false;
  public submitted = false;
  public listCompany: any[] = [];
  public listUnit: any[] = [];
  public listPosition: any[] = [];
  public readonly statuses = EMPLOYEE_STATUSES;
  public readonly genders = GENDERS;

  constructor(
    private formBuilder: UntypedFormBuilder,
    private employeeService: EmployeeService,
    private companyService: CompanyService,
    private organizationUnitService: OrganizationUnitService,
    private positionService: PositionService,
    private notify: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.formGroup = this.formBuilder.group({
      id: [STRING_EMPTY],
      companyId: [null],
      organizationUnitId: [null],
      positionId: [null],
      code: [STRING_EMPTY, [Validators.required]],
      fullName: [STRING_EMPTY, [Validators.required]],
      dateOfBirth: [null],
      gender: [null],
      phone: [STRING_EMPTY],
      email: [STRING_EMPTY, [Validators.email]],
      address: [STRING_EMPTY],
      hireDate: [null],
      status: ['Active', [Validators.required]],
    });
    this.getLists();
    if (this.data) {
      this.isLoading = true;
      this.employeeService.getById(this.data.id).subscribe(
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

  private getLists(): void {
    this.isLoadingList = true;
    forkJoin([
      this.companyService.getAllData(),
      this.organizationUnitService.getAllData(),
      this.positionService.getAllData(),
    ]).subscribe(
      ([companies, units, positions]) => {
        this.listCompany = companies.responseCode === 200 ? companies.listData ?? [] : [];
        this.listUnit = (units.responseCode === 200 ? units.listData ?? [] : [])
          .map((x: any) => ({ ...x, label: x.code ? `${x.code} – ${x.name}` : x.name }))
          .sort((a: any, b: any) => (a.code ?? '').localeCompare(b.code ?? ''));
        this.listPosition = positions.responseCode === 200 ? positions.listData ?? [] : [];
        if (!this.data && this.listCompany.length === 1 && !this.formGroup.value.companyId) {
          this.formGroup.patchValue({ companyId: this.listCompany[0].id });
        }
      },
      () => {
        this.isLoadingList = false;
        this.notify.error();
      },
      () => {
        this.isLoadingList = false;
      }
    );
  }

  // Unit picked → company of that unit (keeps Employee.CompanyId consistent)
  public onChangeUnit(id: any): void {
    const unit = this.listUnit.find((x) => x.id === id);
    if (unit?.companyId) {
      this.formGroup.patchValue({ companyId: unit.companyId });
    }
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
      organizationUnitId: v.organizationUnitId ?? STRING_EMPTY,
      positionId: v.positionId ?? STRING_EMPTY,
      code: (v.code ?? STRING_EMPTY).trim(),
      fullName: (v.fullName ?? STRING_EMPTY).trim(),
      dateOfBirth: toDateParam(v.dateOfBirth),
      gender: v.gender ?? STRING_EMPTY,
      phone: v.phone ?? STRING_EMPTY,
      email: (v.email ?? STRING_EMPTY).trim(),
      address: v.address ?? STRING_EMPTY,
      hireDate: toDateParam(v.hireDate),
      status: v.status ?? STRING_EMPTY,
    };
    this.isLoading = true;
    const formData = toFormData(params);
    const request = params.id ? this.employeeService.update(formData) : this.employeeService.insert(formData);
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
