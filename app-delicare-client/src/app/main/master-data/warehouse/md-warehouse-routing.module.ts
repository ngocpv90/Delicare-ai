import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdWarehouseComponent } from './md-warehouse.component';

const routes: Routes = [
  {
    path: '',
    component: MdWarehouseComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdWarehouseRoutingModule {}
