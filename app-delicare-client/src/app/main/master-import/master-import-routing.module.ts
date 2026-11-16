import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MasterImportComponent } from './master-import.component';

const routes: Routes = [
  {
    path: '',
    component: MasterImportComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MasterImportRoutingModule {}
