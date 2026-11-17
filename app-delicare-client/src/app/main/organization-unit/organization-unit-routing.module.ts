import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { OrganizationUnitComponent } from './organization-unit.component';

const routes: Routes = [
  {
    path: '',
    component: OrganizationUnitComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class OrganizationUnitRoutingModule {}
