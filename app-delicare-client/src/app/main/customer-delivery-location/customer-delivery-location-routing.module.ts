import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CustomerDeliveryLocationComponent } from './customer-delivery-location.component';

const routes: Routes = [
  {
    path: '',
    component: CustomerDeliveryLocationComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class CustomerDeliveryLocationRoutingModule {}
