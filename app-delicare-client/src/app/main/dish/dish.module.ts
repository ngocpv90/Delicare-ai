import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { DishModalComponent } from './dish-modal/dish-modal.component';
import { DishRoutingModule } from './dish-routing.module';
import { DishComponent } from './dish.component';

@NgModule({
  declarations: [DishComponent, DishModalComponent],
  imports: [
    SharedUiModule,
    DishRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class DishModule {}
