import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdStorageLocationModalComponent } from './md-storage-location-modal/md-storage-location-modal.component';
import { MdStorageLocationRoutingModule } from './md-storage-location-routing.module';
import { MdStorageLocationComponent } from './md-storage-location.component';

@NgModule({
  declarations: [MdStorageLocationComponent, MdStorageLocationModalComponent],
  imports: [
    SharedUiModule,
    MdStorageLocationRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdStorageLocationModule {}
