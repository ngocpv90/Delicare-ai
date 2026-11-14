import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdEmployeeComponent } from './md-employee.component';

const routes: Routes = [
  {
    path: '',
    component: MdEmployeeComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdEmployeeRoutingModule {}
