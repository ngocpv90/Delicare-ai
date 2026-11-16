import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { SupplierMaterialComponent } from './supplier-material.component';

const routes: Routes = [
  {
    path: '',
    component: SupplierMaterialComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class SupplierMaterialRoutingModule {}
