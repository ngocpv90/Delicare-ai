import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdEmployeeModalComponent } from './md-employee-modal/md-employee-modal.component';
import { MdEmployeeRoutingModule } from './md-employee-routing.module';
import { MdEmployeeComponent } from './md-employee.component';

@NgModule({
  declarations: [MdEmployeeComponent, MdEmployeeModalComponent],
  imports: [
    SharedUiModule,
    MdEmployeeRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdEmployeeModule {}
