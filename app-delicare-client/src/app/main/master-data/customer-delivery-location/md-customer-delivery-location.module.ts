import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdCustomerDeliveryLocationModalComponent } from './md-customer-delivery-location-modal/md-customer-delivery-location-modal.component';
import { MdCustomerDeliveryLocationRoutingModule } from './md-customer-delivery-location-routing.module';
import { MdCustomerDeliveryLocationComponent } from './md-customer-delivery-location.component';

@NgModule({
  declarations: [MdCustomerDeliveryLocationComponent, MdCustomerDeliveryLocationModalComponent],
  imports: [
    SharedUiModule,
    MdCustomerDeliveryLocationRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdCustomerDeliveryLocationModule {}
