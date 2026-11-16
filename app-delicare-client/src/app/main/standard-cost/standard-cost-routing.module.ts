import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { StandardCostComponent } from './standard-cost.component';

const routes: Routes = [
  {
    path: '',
    component: StandardCostComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class StandardCostRoutingModule {}
