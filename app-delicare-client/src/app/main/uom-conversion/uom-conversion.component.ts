import { Component } from '@angular/core';
import { forkJoin, map } from 'rxjs';
import { toDateParam } from 'src/app/helpers/common-helper';
import { ACTIVE_STATUSES, labelOf } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { UomConversionService } from 'src/app/services/uom-conversion.service';
import { labelFrom, labelMap } from 'src/app/shared/master/local-query';
import { MasterListBase } from 'src/app/shared/master/master-list.base';
import { UomConversionModalComponent } from './uom-conversion-modal/uom-conversion-modal.component';

// scm-uom-conversion – Quy đổi đơn vị (FE-2): 1 [Từ đơn vị] = hệ số × [Sang đơn vị].
// Box "Thử quy đổi" calls UomConversion/Convert (xuôi, ngược, qua đơn vị trung gian, chỉ quy đổi đang hiệu lực).
@Component({
  selector: 'app-uom-conversion',
  templateUrl: './uom-conversion.component.html',
})
export class UomConversionComponent extends MasterListBase {
  public readonly statuses = ACTIVE_STATUSES;
  public readonly labelOf = labelOf;
  public listUnit: any[] = [];
  protected searchFields = ['fromUnitName', 'toUnitName', 'note'];
  protected override filterDefaults = { fromUnitId: null, toUnitId: null, status: '' };

  // Thử quy đổi
  public tryQuantity: number | null = 1;
  public tryFromUnitId: string | null = null;
  public tryToUnitId: string | null = null;
  public tryOn: Date | null = null;
  public tryResult: number | null = null;
  public tryError = '';
  public isConverting = false;

  constructor(
    private uomConversionService: UomConversionService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected loadRows() {
    return forkJoin([this.uomConversionService.getAllData(), this.lookup.units()]).pipe(
      map(([res, units]) => {
        this.listUnit = units;
        const names = labelMap(units, (x) => x.label);
        const list: any[] = res?.responseCode === 200 ? res.listData ?? [] : [];
        return list.map((x) => ({
          ...x,
          fromUnitName: labelFrom(names, x.fromUnitId),
          toUnitName: labelFrom(names, x.toUnitId),
        }));
      })
    );
  }

  protected deleteRow(id: any) {
    return this.uomConversionService.delete(id);
  }

  public openModal(data?: any): void {
    this.openForm(UomConversionModalComponent, data);
  }

  public unitName(id: any): string {
    return this.listUnit.find((x) => x.id === id)?.label ?? '';
  }

  public convert(): void {
    this.tryResult = null;
    this.tryError = '';
    if (this.tryQuantity === null || !this.tryFromUnitId || !this.tryToUnitId) {
      this.tryError = 'Nhập số lượng và chọn 2 đơn vị';
      return;
    }
    this.isConverting = true;
    this.uomConversionService
      .convert({
        quantity: this.tryQuantity,
        fromUnitId: this.tryFromUnitId,
        toUnitId: this.tryToUnitId,
        on: toDateParam(this.tryOn) || undefined,
      })
      .subscribe({
        next: (res) => {
          this.isConverting = false;
          if (res?.responseCode === 200) {
            this.tryResult = res.data;
          } else {
            this.tryError = res?.responseMessage || 'Không quy đổi được';
          }
        },
        error: () => {
          this.isConverting = false;
          this.tryError = 'Không quy đổi được';
          this.notify.error();
        },
      });
  }
}
