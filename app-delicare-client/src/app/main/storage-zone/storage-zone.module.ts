import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { StorageZoneModalComponent } from './storage-zone-modal/storage-zone-modal.component';
import { StorageZoneRoutingModule } from './storage-zone-routing.module';
import { StorageZoneComponent } from './storage-zone.component';

@NgModule({
  declarations: [StorageZoneComponent, StorageZoneModalComponent],
  imports: [
    SharedUiModule,
    StorageZoneRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class StorageZoneModule {}
