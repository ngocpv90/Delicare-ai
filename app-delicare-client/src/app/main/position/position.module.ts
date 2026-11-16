import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { PositionModalComponent } from './position-modal/position-modal.component';
import { PositionRoutingModule } from './position-routing.module';
import { PositionComponent } from './position.component';

@NgModule({
  declarations: [PositionComponent, PositionModalComponent],
  imports: [
    SharedUiModule,
    PositionRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class PositionModule {}
