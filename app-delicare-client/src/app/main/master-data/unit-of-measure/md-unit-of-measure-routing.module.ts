import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MdUnitOfMeasureComponent } from './md-unit-of-measure.component';

const routes: Routes = [
  {
    path: '',
    component: MdUnitOfMeasureComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class MdUnitOfMeasureRoutingModule {}
