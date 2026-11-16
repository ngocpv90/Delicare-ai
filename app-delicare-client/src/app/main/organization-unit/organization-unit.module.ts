import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { OrganizationUnitModalComponent } from './organization-unit-modal/organization-unit-modal.component';
import { OrganizationUnitRoutingModule } from './organization-unit-routing.module';
import { OrganizationUnitComponent } from './organization-unit.component';

@NgModule({
  declarations: [OrganizationUnitComponent, OrganizationUnitModalComponent],
  imports: [
    SharedUiModule,
    OrganizationUnitRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class OrganizationUnitModule {}
