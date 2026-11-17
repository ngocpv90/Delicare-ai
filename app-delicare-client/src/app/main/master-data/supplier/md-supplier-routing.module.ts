import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdSupplierComponent } from './md-supplier.component';

const routes: Routes = [
  {
    path: '',
    component: MdSupplierComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdSupplierRoutingModule {}
