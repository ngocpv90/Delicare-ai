import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdIngredientCategoryModalComponent } from './md-ingredient-category-modal/md-ingredient-category-modal.component';
import { MdIngredientCategoryRoutingModule } from './md-ingredient-category-routing.module';
import { MdIngredientCategoryComponent } from './md-ingredient-category.component';

@NgModule({
  declarations: [MdIngredientCategoryComponent, MdIngredientCategoryModalComponent],
  imports: [
    SharedUiModule,
    MdIngredientCategoryRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdIngredientCategoryModule {}
