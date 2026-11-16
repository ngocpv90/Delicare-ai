import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { PricePolicyComponent } from './price-policy.component';

const routes: Routes = [
  {
    path: '',
    component: PricePolicyComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class PricePolicyRoutingModule {}
