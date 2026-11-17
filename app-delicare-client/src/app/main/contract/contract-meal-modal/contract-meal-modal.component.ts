import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ACTIVE_STATUSES, CodeLabel, toNumberParam } from 'src/app/helpers/master-data-options';
import { ContractMealService } from 'src/app/services/contract-meal.service';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { MetaDataService } from 'src/app/services/meta-data.service';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// ContractMealRequest: ContractId, MealType, MealName, UnitPrice, UnitId, Status, ServiceType.
// Standalone: opened from the crm-contract drawer (tab "Suất ăn") and from the crm-contract-meal list.
// Open with data = row (edit) or { contractId } (create for that contract, contract locked).
@Component({
  selector: 'app-contract-meal-modal',
  standalone: true,
  imports: [SharedUiModule, LoadingModalModule],
  templateUrl: './contract-meal-modal.component.html',
})
export class ContractMealModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly serviceSuggestions = ['Giao tận nơi', 'Chia suất tại chỗ', 'Tự lấy tại bếp'];
  public mealTypes: CodeLabel[] = [];
  public listContract: any[] = [];
  public listUnit: any[] = [];
  protected requestFields = ['contractId', 'mealType', 'mealName', 'unitPrice', 'unitId', 'status', 'serviceType'];

  constructor(
    protected api: ContractMealService,
    private lookup: MasterLookupService,
    private metaDataService: MetaDataService
  ) {
    super();
  }

  // Contract fixed when the modal is opened for one contract
  public get contractLocked(): boolean {
    return !this.isEdit && !!this.data?.contractId && !!this.data?.lockContract;
  }

  protected buildForm() {
    return this.formBuilder.group({
      contractId: [null, [Validators.required]],
      mealType: [null, [Validators.required]],
      mealName: ['', [Validators.required]],
      serviceType: [''],
      unitPrice: [null, [Validators.required, Validators.min(0)]],
      unitId: [null],
      status: ['Active', [Validators.required]],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.metaDataService.getReferenceList('MealType').subscribe((list) => (this.mealTypes = list));
    forkJoin([this.lookup.contracts(), this.lookup.units()]).subscribe(([contracts, units]) => {
      this.listContract = contracts;
      this.listUnit = units;
      // default unit "suất" when it exists
      if (!this.isEdit && !this.formGroup.value.unitId) {
        const portion = units.find((x) => ['suat', 'suất'].includes(String(x.code ?? x.name ?? '').toLowerCase()));
        if (portion) {
          this.formGroup.patchValue({ unitId: portion.id });
        }
      }
      this.isLoadingList = false;
    });
    if (this.contractLocked) {
      this.formGroup.controls['contractId'].disable();
    }
  }

  protected override toParams(v: any) {
    return { ...v, mealName: (v.mealName ?? '').trim(), unitPrice: toNumberParam(v.unitPrice) };
  }
}
