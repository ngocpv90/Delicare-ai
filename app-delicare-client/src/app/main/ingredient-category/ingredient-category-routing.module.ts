import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { IngredientCategoryComponent } from './ingredient-category.component';

const routes: Routes = [
  {
    path: '',
    component: IngredientCategoryComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class IngredientCategoryRoutingModule {}
