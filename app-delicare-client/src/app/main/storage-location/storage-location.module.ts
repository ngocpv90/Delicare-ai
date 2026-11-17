import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { StorageLocationModalComponent } from './storage-location-modal/storage-location-modal.component';
import { StorageLocationRoutingModule } from './storage-location-routing.module';
import { StorageLocationComponent } from './storage-location.component';

@NgModule({
  declarations: [StorageLocationComponent, StorageLocationModalComponent],
  imports: [
    SharedUiModule,
    StorageLocationRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class StorageLocationModule {}
