import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DishCategoryComponent } from './dish-category.component';

const routes: Routes = [
  {
    path: '',
    component: DishCategoryComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class DishCategoryRoutingModule {}
