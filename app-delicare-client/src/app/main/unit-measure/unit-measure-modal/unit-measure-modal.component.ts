import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES, UOM_TYPES, toNumberParam } from 'src/app/helpers/master-data-options';
import { UnitMeasureService } from 'src/app/services/unit-measure.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// UnitMeasureRequest: Name, Code, UomType, DecimalPrecision, Status, Symbol, UnitType.
// FE-2 adds the fields the old form missed (Update overwrites every column). UnitType is an older duplicate
// of UomType: kept from the record, filled with UomType when empty (PROPOSED – BA/PO CONFIRM).
@Component({
  selector: 'app-unit-measure-modal',
  templateUrl: './unit-measure-modal.component.html',
})
export class UnitMeasureModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly uomTypes = UOM_TYPES;
  protected requestFields = ['name', 'code', 'uomType', 'decimalPrecision', 'status', 'symbol', 'unitType'];

  constructor(protected api: UnitMeasureService) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required, Validators.maxLength(255)]],
      symbol: [''],
      uomType: [null, [Validators.required]],
      decimalPrecision: [0, [Validators.min(0), Validators.max(6)]],
      status: ['Active', [Validators.required]],
    });
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      symbol: (v.symbol ?? '').trim(),
      decimalPrecision: toNumberParam(v.decimalPrecision),
      unitType: this.record?.unitType || v.uomType,
    };
  }
}
