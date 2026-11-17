import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdStorageZoneComponent } from './md-storage-zone.component';

const routes: Routes = [
  {
    path: '',
    component: MdStorageZoneComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdStorageZoneRoutingModule {}
