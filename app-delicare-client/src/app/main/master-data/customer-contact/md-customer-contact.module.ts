import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdCustomerContactModalComponent } from './md-customer-contact-modal/md-customer-contact-modal.component';
import { MdCustomerContactRoutingModule } from './md-customer-contact-routing.module';
import { MdCustomerContactComponent } from './md-customer-contact.component';

@NgModule({
  declarations: [MdCustomerContactComponent, MdCustomerContactModalComponent],
  imports: [
    SharedUiModule,
    MdCustomerContactRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdCustomerContactModule {}
