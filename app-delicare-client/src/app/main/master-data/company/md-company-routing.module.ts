import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdCompanyComponent } from './md-company.component';

const routes: Routes = [
  {
    path: '',
    component: MdCompanyComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdCompanyRoutingModule {}
