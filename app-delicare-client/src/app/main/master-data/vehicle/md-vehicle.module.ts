import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdVehicleModalComponent } from './md-vehicle-modal/md-vehicle-modal.component';
import { MdVehicleRoutingModule } from './md-vehicle-routing.module';
import { MdVehicleComponent } from './md-vehicle.component';

@NgModule({
  declarations: [MdVehicleComponent, MdVehicleModalComponent],
  imports: [
    SharedUiModule,
    MdVehicleRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdVehicleModule {}
