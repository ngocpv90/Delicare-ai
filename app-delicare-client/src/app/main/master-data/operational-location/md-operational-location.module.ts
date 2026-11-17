import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdOperationalLocationModalComponent } from './md-operational-location-modal/md-operational-location-modal.component';
import { MdOperationalLocationRoutingModule } from './md-operational-location-routing.module';
import { MdOperationalLocationComponent } from './md-operational-location.component';

@NgModule({
  declarations: [MdOperationalLocationComponent, MdOperationalLocationModalComponent],
  imports: [
    SharedUiModule,
    MdOperationalLocationRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdOperationalLocationModule {}
