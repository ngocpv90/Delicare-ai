import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdDishCategoryModalComponent } from './md-dish-category-modal/md-dish-category-modal.component';
import { MdDishCategoryRoutingModule } from './md-dish-category-routing.module';
import { MdDishCategoryComponent } from './md-dish-category.component';

@NgModule({
  declarations: [MdDishCategoryComponent, MdDishCategoryModalComponent],
  imports: [
    SharedUiModule,
    MdDishCategoryRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdDishCategoryModule {}
