import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { CustomerDeliveryLocationModalComponent } from './customer-delivery-location-modal/customer-delivery-location-modal.component';
import { CustomerDeliveryLocationRoutingModule } from './customer-delivery-location-routing.module';
import { CustomerDeliveryLocationComponent } from './customer-delivery-location.component';

@NgModule({
  declarations: [CustomerDeliveryLocationComponent, CustomerDeliveryLocationModalComponent],
  imports: [
    SharedUiModule,
    CustomerDeliveryLocationRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class CustomerDeliveryLocationModule {}
