import { Component } from '@angular/core';
import { Validators } from '@angular/forms';
import { VEHICLE_STATUSES, toNumberParam } from 'src/app/helpers/master-data-options';
import { VehicleService } from 'src/app/services/vehicle.service';
import { MasterModalBase } from 'src/app/shared/master/master-modal.base';

// VehicleRequest: Code, LicensePlate, VehicleType, Capacity, Status, Note. VehicleType is free text
// (suggestions from the mockup demo data – PROPOSED).
@Component({
  selector: 'app-vehicle-modal',
  templateUrl: './vehicle-modal.component.html',
})
export class VehicleModalComponent extends MasterModalBase {
  public readonly statuses = VEHICLE_STATUSES;
  public readonly typeSuggestions = ['Xe tải lạnh', 'Xe giữ nhiệt', 'Xe tải thường', 'Xe van', 'Xe máy'];
  protected requestFields = ['code', 'licensePlate', 'vehicleType', 'capacity', 'status', 'note'];

  constructor(protected api: VehicleService) {
    super();
  }

  protected buildForm() {
    return this.formBuilder.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      licensePlate: ['', [Validators.required]],
      vehicleType: [''],
      capacity: [null, [Validators.min(0)]],
      status: ['Active', [Validators.required]],
      note: [''],
    });
  }

  protected override toParams(v: any) {
    return {
      ...v,
      code: (v.code ?? '').trim(),
      licensePlate: (v.licensePlate ?? '').trim().toUpperCase(),
      vehicleType: (v.vehicleType ?? '').trim(),
      capacity: toNumberParam(v.capacity),
    };
  }
}
