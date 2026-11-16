import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { ShiftModalComponent } from './shift-modal/shift-modal.component';
import { ShiftRoutingModule } from './shift-routing.module';
import { ShiftComponent } from './shift.component';

@NgModule({
  declarations: [ShiftComponent, ShiftModalComponent],
  imports: [
    SharedUiModule,
    ShiftRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class ShiftModule {}
