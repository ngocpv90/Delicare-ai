import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdDishCategoryComponent } from './md-dish-category.component';

const routes: Routes = [
  {
    path: '',
    component: MdDishCategoryComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdDishCategoryRoutingModule {}
