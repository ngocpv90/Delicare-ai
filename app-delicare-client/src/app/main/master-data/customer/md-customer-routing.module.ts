import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdCustomerComponent } from './md-customer.component';

const routes: Routes = [
  {
    path: '',
    component: MdCustomerComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdCustomerRoutingModule {}
