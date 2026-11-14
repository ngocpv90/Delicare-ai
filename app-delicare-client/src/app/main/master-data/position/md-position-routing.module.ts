import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdPositionComponent } from './md-position.component';

const routes: Routes = [
  {
    path: '',
    component: MdPositionComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdPositionRoutingModule {}
