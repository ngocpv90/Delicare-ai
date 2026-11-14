import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdOrganizationUnitModalComponent } from './md-organization-unit-modal/md-organization-unit-modal.component';
import { MdOrganizationUnitRoutingModule } from './md-organization-unit-routing.module';
import { MdOrganizationUnitComponent } from './md-organization-unit.component';

@NgModule({
  declarations: [MdOrganizationUnitComponent, MdOrganizationUnitModalComponent],
  imports: [
    SharedUiModule,
    MdOrganizationUnitRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdOrganizationUnitModule {}
