import { Component, Input, OnInit } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { forkJoin } from 'rxjs';
import { toFormData } from 'src/app/helpers/common-helper';
import { STRING_EMPTY } from 'src/app/helpers/constant-helper';
import { CompanyService } from 'src/app/services/company.service';
import { EmployeeService } from 'src/app/services/employee.service';
import { OrganizationUnitService } from 'src/app/services/organization-unit.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { RECORD_STATUSES, UNIT_TYPES } from '../org-options';

// adm-orgunit create / edit. data = { id } (edit) or { parentId } (new child).
// Sends every OrganizationUnitRequest field (Update overwrites): Id, CompanyId (NOT NULL), ParentId, Code, Name,
// UnitType, Address, Phone, Status, Note, ManagerId (→ Employee). The API blocks duplicate codes and parent cycles.
@Component({
  selector: 'app-organization-unit-modal',
  templateUrl: './organization-unit-modal.component.html',
})
export class OrganizationUnitModalComponent implements OnInit {
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public formGroup!: UntypedFormGroup;
  public isLoading = false;
  public isLoadingList = false;
  public submitted = false;
  public listCompany: any[] = [];
  public listParent: any[] = [];
  public listEmployee: any[] = [];
  public readonly unitTypes = UNIT_TYPES;
  public readonly statuses = RECORD_STATUSES;

  constructor(
    private formBuilder: UntypedFormBuilder,
    private organizationUnitService: OrganizationUnitService,
    private companyService: CompanyService,
    private employeeService: EmployeeService,
    private notify: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.formGroup = this.formBuilder.group({
      id: [STRING_EMPTY],
      companyId: [null, [Validators.required]],
      parentId: [this.data?.parentId ?? null],
      code: [STRING_EMPTY, [Validators.required]],
      name: [STRING_EMPTY, [Validators.required]],
      unitType: [null, [Validators.required]],
      managerId: [null],
      address: [STRING_EMPTY],
      phone: [STRING_EMPTY],
      status: ['Active', [Validators.required]],
      note: [STRING_EMPTY],
    });
    this.getLists();
    if (this.data?.id) {
      this.isLoading = true;
      this.organizationUnitService.getById(this.data.id).subscribe(
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
      this.employeeService.getAllData(),
    ]).subscribe(
      ([companies, units, employees]) => {
        this.listCompany = companies.responseCode === 200 ? companies.listData ?? [] : [];
        this.listParent = (units.responseCode === 200 ? units.listData ?? [] : [])
          .filter((x: any) => x.id !== this.data?.id)
          .map((x: any) => ({ ...x, label: x.code ? `${x.code} – ${x.name}` : x.name }))
          .sort((a: any, b: any) => (a.code ?? '').localeCompare(b.code ?? ''));
        this.listEmployee = (employees.responseCode === 200 ? employees.listData ?? [] : []).map((x: any) => ({
          ...x,
          label: x.code ? `${x.fullName} (${x.code})` : x.fullName,
        }));
        // One company (pilot): preselect it for a new unit; a child takes its parent's company
        if (!this.data?.id && !this.formGroup.value.companyId) {
          const parent = this.listParent.find((x) => x.id === this.data?.parentId);
          const companyId = parent?.companyId ?? (this.listCompany.length === 1 ? this.listCompany[0].id : null);
          this.formGroup.patchValue({ companyId });
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
      companyId: v.companyId,
      parentId: v.parentId ?? STRING_EMPTY,
      code: (v.code ?? STRING_EMPTY).trim(),
      name: (v.name ?? STRING_EMPTY).trim(),
      unitType: v.unitType ?? STRING_EMPTY,
      address: v.address ?? STRING_EMPTY,
      phone: v.phone ?? STRING_EMPTY,
      status: v.status ?? STRING_EMPTY,
      note: v.note ?? STRING_EMPTY,
      managerId: v.managerId ?? STRING_EMPTY,
    };
    this.isLoading = true;
    const formData = toFormData(params);
    const request = params.id
      ? this.organizationUnitService.update(formData)
      : this.organizationUnitService.insert(formData);
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
