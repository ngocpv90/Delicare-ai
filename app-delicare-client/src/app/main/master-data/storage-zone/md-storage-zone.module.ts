import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdStorageZoneModalComponent } from './md-storage-zone-modal/md-storage-zone-modal.component';
import { MdStorageZoneRoutingModule } from './md-storage-zone-routing.module';
import { MdStorageZoneComponent } from './md-storage-zone.component';

@NgModule({
  declarations: [MdStorageZoneComponent, MdStorageZoneModalComponent],
  imports: [
    SharedUiModule,
    MdStorageZoneRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdStorageZoneModule {}
