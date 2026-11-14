import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdSupplierModalComponent } from './md-supplier-modal/md-supplier-modal.component';
import { MdSupplierRoutingModule } from './md-supplier-routing.module';
import { MdSupplierComponent } from './md-supplier.component';

@NgModule({
  declarations: [MdSupplierComponent, MdSupplierModalComponent],
  imports: [
    SharedUiModule,
    MdSupplierRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdSupplierModule {}
