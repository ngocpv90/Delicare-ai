import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdCustomerDeliveryLocationComponent } from './md-customer-delivery-location.component';

const routes: Routes = [
  {
    path: '',
    component: MdCustomerDeliveryLocationComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdCustomerDeliveryLocationRoutingModule {}
