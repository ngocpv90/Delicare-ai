import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { ACTIVE_STATUSES, timeText, toTimeParam } from 'src/app/helpers/master-data-options';
import { MasterLookupService } from 'src/app/services/master-lookup.service';
import { ShiftService } from 'src/app/services/shift.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// ShiftRequest: CompanyId, Code, Name, StartTime, EndTime, Status, Note. Ca qua đêm (kết thúc < bắt đầu) hợp lệ.
@Component({
  selector: 'app-shift-modal',
  templateUrl: './shift-modal.component.html',
})
export class ShiftModalComponent extends MasterModalBase {
  public readonly statuses = ACTIVE_STATUSES;
  public listCompany: any[] = [];
  protected requestFields = ['companyId', 'code', 'name', 'startTime', 'endTime', 'status', 'note'];

  constructor(
    protected api: ShiftService,
    private lookup: MasterLookupService
  ) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      companyId: [null],
      code: ['', [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.required]],
      startTime: ['', [Validators.required]],
      endTime: ['', [Validators.required]],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override loadLists(): void {
    this.isLoadingList = true;
    this.lookup.companies().subscribe((list) => {
      this.listCompany = list;
      if (!this.isEdit && list.length === 1 && !this.formGroup.value.companyId) {
        this.formGroup.patchValue({ companyId: list[0].id });
      }
      this.isLoadingList = false;
    });
  }

  protected override patchRecord(record: any): void {
    this.formGroup.patchValue({ ...record, startTime: timeText(record.startTime), endTime: timeText(record.endTime) });
  }

  public get overnight(): boolean {
    const { startTime, endTime } = this.formGroup.value;
    return !!startTime && !!endTime && endTime < startTime;
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      name: (v.name ?? '').trim(),
      startTime: toTimeParam(v.startTime),
      endTime: toTimeParam(v.endTime),
    };
  }
}
