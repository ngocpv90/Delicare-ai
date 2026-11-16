import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { DishCategoryModalComponent } from './dish-category-modal/dish-category-modal.component';
import { DishCategoryRoutingModule } from './dish-category-routing.module';
import { DishCategoryComponent } from './dish-category.component';

@NgModule({
  declarations: [DishCategoryComponent, DishCategoryModalComponent],
  imports: [
    SharedUiModule,
    DishCategoryRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class DishCategoryModule {}
