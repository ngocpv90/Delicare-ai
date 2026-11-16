import { Component } from '@angular/core';
import { map } from 'rxjs';
import { ACTIVE_STATUSES, UOM_TYPES, labelOf } from 'src/app/helpers/master-data-options';
import { UnitMeasureService } from 'src/app/services/unit-measure.service';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { UnitMeasureModalComponent } from './unit-measure-modal/unit-measure-modal.component';

// scm-uom – Đơn vị tính. FE-2: list from UnitMeasure/GetAllData (uspUnitMeasure_GetAll only returns
// Id + Name), with Code / Symbol / UomType / DecimalPrecision / Status.
@Component({
  selector: 'app-unit-measure',
  templateUrl: './unit-measure.component.html',
})
export class UnitMeasureComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly uomTypes = UOM_TYPES;
  public readonly labelOf = labelOf;
  protected searchFields = ['code', 'name', 'symbol'];
  protected override filterDefaults = { uomType: null, status: '' };

  constructor(private unitMeasureService: UnitMeasureService) {
    super();
  }

  protected loadRows() {
    return this.unitMeasureService
      .getAllData()
      .pipe(map((res) => (res?.responseCode === 200 ? res.listData ?? [] : [])));
  }

  protected deleteRow(id: any) {
    return this.unitMeasureService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(UnitMeasureModalComponent, data);
  }
}
