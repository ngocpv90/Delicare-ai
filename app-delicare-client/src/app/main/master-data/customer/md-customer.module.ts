import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdCustomerModalComponent } from './md-customer-modal/md-customer-modal.component';
import { MdCustomerRoutingModule } from './md-customer-routing.module';
import { MdCustomerComponent } from './md-customer.component';

@NgModule({
  declarations: [MdCustomerComponent, MdCustomerModalComponent],
  imports: [
    SharedUiModule,
    MdCustomerRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdCustomerModule {}
