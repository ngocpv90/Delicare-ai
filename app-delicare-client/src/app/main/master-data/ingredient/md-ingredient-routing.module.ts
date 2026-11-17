import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdIngredientComponent } from './md-ingredient.component';

const routes: Routes = [
  {
    path: '',
    component: MdIngredientComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdIngredientRoutingModule {}
