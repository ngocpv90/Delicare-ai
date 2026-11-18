import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdSupplierMaterialModalComponent } from './md-supplier-material-modal/md-supplier-material-modal.component';
import { MdSupplierMaterialRoutingModule } from './md-supplier-material-routing.module';
import { MdSupplierMaterialComponent } from './md-supplier-material.component';

@NgModule({
  declarations: [MdSupplierMaterialComponent, MdSupplierMaterialModalComponent],
  imports: [
    SharedUiModule,
    MdSupplierMaterialRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdSupplierMaterialModule {}
