import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { InventoryPolicyModalComponent } from './inventory-policy-modal/inventory-policy-modal.component';
import { InventoryPolicyRoutingModule } from './inventory-policy-routing.module';
import { InventoryPolicyComponent } from './inventory-policy.component';

@NgModule({
  declarations: [InventoryPolicyComponent, InventoryPolicyModalComponent],
  imports: [
    SharedUiModule,
    InventoryPolicyRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class InventoryPolicyModule {}
