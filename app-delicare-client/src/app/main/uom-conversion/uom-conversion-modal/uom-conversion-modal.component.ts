import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { checkValidateBetweenTwoDate, toDateParam } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, toNumberParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { UomConversionService } from 'src/app/services/uom-conversion.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// UomConversionRequest: FromUnitId, ToUnitId, ConversionFactor, EffectiveFrom, EffectiveTo, IsActive, Note, Status.
// Backend rules (400): hệ số > 0, 2 đơn vị khác nhau, không trùng cặp.
@Component({
  selector: 'app-uom-conversion-modal',
  templateUrl: './uom-conversion-modal.component.html',
})
export class UomConversionModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listUnit: any[] = [];
  protected requestFields = ['fromUnitId', 'toUnitId', 'conversionFactor', 'effectiveFrom', 'effectiveTo', 'isActive', 'note', 'status'];

  constructor(
    protected api: UomConversionService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      fromUnitId: [null, [Validators.required]],
      toUnitId: [null, [Validators.required]],
      conversionFactor: [null, [Validators.required]],
      effectiveFrom: [null],
      effectiveTo: [null],
      isActive: [true],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.lookup.units().subscribe((list) => {
      this.listUnit = list;
      this.isLoadingList = false;
    });
  }

  public unitName(id: any): string {
    return this.listUnit.find((x) => x.id === id)?.label ?? '…';
  }

  protected override validate(v: any): string | null {
    if (!(Number(v.conversionFactor) > 0)) {
      return 'Hệ số quy đổi phải lớn hơn 0';
    }
    if (v.fromUnitId === v.toUnitId) {
      return 'Hai đơn vị phải khác nhau';
    }
    if (checkValidateBetweenTwoDate(v.effectiveFrom, v.effectiveTo)) {
      return 'Ngày hiệu lực đến phải sau ngày hiệu lực từ';
    }
    return null;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      conversionFactor: toNumberParam(v.conversionFactor),
      effectiveFrom: toDateParam(v.effectiveFrom),
      effectiveTo: toDateParam(v.effectiveTo),
      isActive: !!v.isActive,
    };
  }
}
