import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdIngredientCategoryComponent } from './md-ingredient-category.component';

const routes: Routes = [
  {
    path: '',
    component: MdIngredientCategoryComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdIngredientCategoryRoutingModule {}
