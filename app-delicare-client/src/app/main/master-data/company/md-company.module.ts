import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdCompanyModalComponent } from './md-company-modal/md-company-modal.component';
import { MdCompanyRoutingModule } from './md-company-routing.module';
import { MdCompanyComponent } from './md-company.component';

@NgModule({
  declarations: [MdCompanyComponent, MdCompanyModalComponent],
  imports: [
    SharedUiModule,
    MdCompanyRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdCompanyModule {}
