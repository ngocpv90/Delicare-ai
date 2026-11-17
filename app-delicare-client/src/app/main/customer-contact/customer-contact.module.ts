import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { CustomerContactModalComponent } from './customer-contact-modal/customer-contact-modal.component';
import { CustomerContactRoutingModule } from './customer-contact-routing.module';
import { CustomerContactComponent } from './customer-contact.component';

@NgModule({
  declarations: [CustomerContactComponent, CustomerContactModalComponent],
  imports: [
    SharedUiModule,
    CustomerContactRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class CustomerContactModule {}
