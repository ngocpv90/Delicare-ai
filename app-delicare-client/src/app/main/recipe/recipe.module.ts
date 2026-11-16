import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { RecipeLineModalComponent } from './recipe-line-modal/recipe-line-modal.component';
import { RecipeModalComponent } from './recipe-modal/recipe-modal.component';
import { RecipeRoutingModule } from './recipe-routing.module';
import { RecipeVersionModalComponent } from './recipe-version-modal/recipe-version-modal.component';
import { RecipeComponent } from './recipe.component';

@NgModule({
  declarations: [RecipeComponent, RecipeModalComponent, RecipeLineModalComponent, RecipeVersionModalComponent],
  imports: [SharedUiModule, RecipeRoutingModule, LoadingModule, NotFoundModule, LoadingModalModule],
})
export class RecipeModule {}
