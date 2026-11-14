import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdSupplierMaterialComponent } from './md-supplier-material.component';

const routes: Routes = [
  {
    path: '',
    component: MdSupplierMaterialComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdSupplierMaterialRoutingModule {}
