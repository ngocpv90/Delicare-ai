import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdDishModalComponent } from './md-dish-modal/md-dish-modal.component';
import { MdDishRoutingModule } from './md-dish-routing.module';
import { MdDishComponent } from './md-dish.component';

@NgModule({
  declarations: [MdDishComponent, MdDishModalComponent],
  imports: [
    SharedUiModule,
    MdDishRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdDishModule {}
