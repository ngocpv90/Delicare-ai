import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { WarehouseModalComponent } from './warehouse-modal/warehouse-modal.component';
import { WarehouseRoutingModule } from './warehouse-routing.module';
import { WarehouseComponent } from './warehouse.component';

@NgModule({
  declarations: [WarehouseComponent, WarehouseModalComponent],
  imports: [
    SharedUiModule,
    WarehouseRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class WarehouseModule {}
