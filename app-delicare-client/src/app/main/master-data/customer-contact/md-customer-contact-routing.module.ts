import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdCustomerContactComponent } from './md-customer-contact.component';

const routes: Routes = [
  {
    path: '',
    component: MdCustomerContactComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdCustomerContactRoutingModule {}
