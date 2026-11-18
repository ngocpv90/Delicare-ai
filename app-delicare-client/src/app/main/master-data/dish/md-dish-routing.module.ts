import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdDishComponent } from './md-dish.component';

const routes: Routes = [
  {
    path: '',
    component: MdDishComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdDishRoutingModule {}
