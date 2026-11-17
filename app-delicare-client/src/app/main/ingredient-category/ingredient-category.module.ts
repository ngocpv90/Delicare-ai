import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { IngredientCategoryModalComponent } from './ingredient-category-modal/ingredient-category-modal.component';
import { IngredientCategoryRoutingModule } from './ingredient-category-routing.module';
import { IngredientCategoryComponent } from './ingredient-category.component';

@NgModule({
  declarations: [IngredientCategoryComponent, IngredientCategoryModalComponent],
  imports: [
    SharedUiModule,
    IngredientCategoryRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class IngredientCategoryModule {}
