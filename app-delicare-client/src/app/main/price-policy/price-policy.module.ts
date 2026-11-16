import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { PricePolicyModalComponent } from './price-policy-modal/price-policy-modal.component';
import { PricePolicyRoutingModule } from './price-policy-routing.module';
import { PricePolicyComponent } from './price-policy.component';

@NgModule({
  declarations: [PricePolicyComponent, PricePolicyModalComponent],
  imports: [SharedUiModule, PricePolicyRoutingModule, LoadingModule, NotFoundModule, LoadingModalModule],
})
export class PricePolicyModule {}
