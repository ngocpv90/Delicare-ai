import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { IngredientModalComponent } from './ingredient-modal/ingredient-modal.component';
import { IngredientRoutingModule } from './ingredient-routing.module';
import { IngredientComponent } from './ingredient.component';

@NgModule({
  declarations: [IngredientComponent, IngredientModalComponent],
  imports: [
    SharedUiModule,
    IngredientRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class IngredientModule {}
