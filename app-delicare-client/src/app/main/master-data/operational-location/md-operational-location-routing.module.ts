import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdOperationalLocationComponent } from './md-operational-location.component';

const routes: Routes = [
  {
    path: '',
    component: MdOperationalLocationComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdOperationalLocationRoutingModule {}
