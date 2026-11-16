import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { InventoryPolicyComponent } from './inventory-policy.component';

const routes: Routes = [
  {
    path: '',
    component: InventoryPolicyComponent,
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class InventoryPolicyRoutingModule {}
