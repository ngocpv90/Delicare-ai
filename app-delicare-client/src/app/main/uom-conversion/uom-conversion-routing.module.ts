import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { UomConversionComponent } from './uom-conversion.component';

const routes: Routes = [
  {
    path: '',
    component: UomConversionComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class UomConversionRoutingModule {}
