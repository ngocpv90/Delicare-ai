import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdVehicleComponent } from './md-vehicle.component';

const routes: Routes = [
  {
    path: '',
    component: MdVehicleComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdVehicleRoutingModule {}
