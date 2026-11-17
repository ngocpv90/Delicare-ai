import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdOrganizationUnitComponent } from './md-organization-unit.component';

const routes: Routes = [
  {
    path: '',
    component: MdOrganizationUnitComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdOrganizationUnitRoutingModule {}
