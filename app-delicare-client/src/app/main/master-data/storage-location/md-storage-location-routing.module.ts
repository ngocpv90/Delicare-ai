import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdStorageLocationComponent } from './md-storage-location.component';

const routes: Routes = [
  {
    path: '',
    component: MdStorageLocationComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdStorageLocationRoutingModule {}
