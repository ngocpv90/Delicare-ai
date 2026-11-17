import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { StorageZoneComponent } from './storage-zone.component';

const routes: Routes = [
  {
    path: '',
    component: StorageZoneComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class StorageZoneRoutingModule {}
