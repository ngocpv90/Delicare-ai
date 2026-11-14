import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdIngredientModalComponent } from './md-ingredient-modal/md-ingredient-modal.component';
import { MdIngredientRoutingModule } from './md-ingredient-routing.module';
import { MdIngredientComponent } from './md-ingredient.component';

@NgModule({
  declarations: [MdIngredientComponent, MdIngredientModalComponent],
  imports: [
    SharedUiModule,
    MdIngredientRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdIngredientModule {}
