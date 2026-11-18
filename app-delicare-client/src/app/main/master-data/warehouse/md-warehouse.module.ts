import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdWarehouseModalComponent } from './md-warehouse-modal/md-warehouse-modal.component';
import { MdWarehouseRoutingModule } from './md-warehouse-routing.module';
import { MdWarehouseComponent } from './md-warehouse.component';

@NgModule({
  declarations: [MdWarehouseComponent, MdWarehouseModalComponent],
  imports: [
    SharedUiModule,
    MdWarehouseRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdWarehouseModule {}
