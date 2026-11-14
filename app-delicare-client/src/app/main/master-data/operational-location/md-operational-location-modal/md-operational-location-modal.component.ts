import { Component, Input, OnInit } from '@angular/core';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { notificationCommon } from 'src/app/helpers/common-helper';
import { ERROR, STRING_EMPTY, SUCCESS } from 'src/app/helpers/constant-helper';
import { MasterDataService } from 'src/app/services/master-data.service';
import { NotifyService } from 'src/app/shared/notify.service';
import { MD_OPTIONS, mdSameId, toMasterFormData } from '../../master-data.constants';

const CONTROLLER = 'OperationalLocation';

interface LookupDef {
  field: string;
  controller: string;
  dependsOn?: string;
  parentKey?: string;
  excludeSelf?: boolean;
}

@Component({
  selector: 'app-md-operational-location-modal',
  templateUrl: './md-operational-location-modal.component.html',
})
export class MdOperationalLocationModalComponent implements OnInit {
  @Input() public title = STRING_EMPTY;
  @Input() public data: any;
  public isLoading = false;
  public submitted = false;
  public formGroup!: UntypedFormGroup;
  public options = MD_OPTIONS;
  public items: Record<string, any[]> = {};
  public loadingLookup: Record<string, boolean> = {};
  private raw: Record<string, any[]> = {};
  private readonly lookups: LookupDef[] = [
    { field: 'companyId', controller: 'Company' },
    { field: 'organizationUnitId', controller: 'OrganizationUnit', dependsOn: 'companyId', parentKey: 'companyId' },
  ];

  constructor(
    private formBuilder: UntypedFormBuilder,
    private masterDataService: MasterDataService,
    private notifyService: NotifyService,
    private activeModal: NgbActiveModal
  ) {}

  public ngOnInit(): void {
    this.formGroup = this.formBuilder.group({
      id: [STRING_EMPTY],
      companyId: [null, [Validators.required]],
      organizationUnitId: [null],
      code: [null, [Validators.required]],
      name: [null, [Validators.required]],
      locationType: [null, [Validators.required]],
      status: ['Active', [Validators.required]],
      contactName: [null],
      contactPhone: [null],
      latitude: [null],
      longitude: [null],
      address: [null, [Validators.required]],
      note: [null],
    });
    this.lookups.forEach((lookup) => {
      this.loadLookup(lookup);
      if (lookup.dependsOn) {
        this.formGroup.get(lookup.dependsOn)?.valueChanges.subscribe(() => this.applyFilter(lookup));
      }
    });
    if (this.data) {
      this.isLoading = true;
      this.masterDataService.getById(CONTROLLER, this.data.id).subscribe(
        (data) => {
          if (data.responseCode === 200 && data.data) {
            this.formGroup.patchValue(data.data);
          }
        },
        () => {
          this.isLoading = false;
          notificationCommon(this.notifyService, ERROR);
        },
        () => {
          this.isLoading = false;
        }
      );
    }
  }

  private loadLookup(lookup: LookupDef): void {
    this.loadingLookup[lookup.field] = true;
    this.masterDataService.getAllData(lookup.controller).subscribe(
      (res) => {
        this.raw[lookup.field] = res?.listData ?? [];
        this.applyFilter(lookup);
      },
      () => {
        this.loadingLookup[lookup.field] = false;
      },
      () => {
        this.loadingLookup[lookup.field] = false;
      }
    );
  }

  // Dependent dropdowns (e.g. khu vực theo kho) show only rows of the selected parent.
  private applyFilter(lookup: LookupDef): void {
    let list = this.raw[lookup.field] ?? [];
    if (lookup.excludeSelf && this.data?.id) {
      list = list.filter((x) => !mdSameId(x.id, this.data.id));
    }
    const parent = lookup.dependsOn ? this.formGroup.get(lookup.dependsOn)?.value : null;
    if (parent && lookup.parentKey) {
      const key = lookup.parentKey;
      list = list.filter((x) => !x[key] || mdSameId(x[key], parent));
    }
    this.items[lookup.field] = list;
  }

  public isInvalid(name: string): boolean {
    const control = this.formGroup.controls[name];
    return control.invalid && (this.submitted || control.dirty || control.touched);
  }

  public destroyModal(reLoad: any): void {
    this.activeModal.close({ reLoad });
  }

  private validate(v: any): string | null {
    
    return null;
  }

  public submitForm(): void {
    this.submitted = true;
    if (this.formGroup.invalid) {
      this.formGroup.markAllAsTouched();
      return;
    }
    const message = this.validate(this.formGroup.value);
    if (message) {
      notificationCommon(this.notifyService, ERROR, message);
      return;
    }
    this.isLoading = true;
    const formData = toMasterFormData(this.formGroup.value);
    const request$ = this.formGroup.value.id
      ? this.masterDataService.update(CONTROLLER, formData)
      : this.masterDataService.insert(CONTROLLER, formData);
    request$.subscribe(
      (data) => {
        if (data.responseCode === 200) {
          notificationCommon(this.notifyService, SUCCESS, data.responseMessage);
          this.destroyModal(true);
        } else {
          notificationCommon(this.notifyService, ERROR, data.responseMessage);
        }
      },
      () => {
        this.isLoading = false;
        notificationCommon(this.notifyService, ERROR);
      },
      () => {
        this.isLoading = false;
      }
    );
  }
}
