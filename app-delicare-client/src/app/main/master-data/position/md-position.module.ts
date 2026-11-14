import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdPositionModalComponent } from './md-position-modal/md-position-modal.component';
import { MdPositionRoutingModule } from './md-position-routing.module';
import { MdPositionComponent } from './md-position.component';

@NgModule({
  declarations: [MdPositionComponent, MdPositionModalComponent],
  imports: [
    SharedUiModule,
    MdPositionRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdPositionModule {}
