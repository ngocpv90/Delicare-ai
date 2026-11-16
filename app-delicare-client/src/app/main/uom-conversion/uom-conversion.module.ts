import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { UomConversionModalComponent } from './uom-conversion-modal/uom-conversion-modal.component';
import { UomConversionRoutingModule } from './uom-conversion-routing.module';
import { UomConversionComponent } from './uom-conversion.component';

@NgModule({
  declarations: [UomConversionComponent, UomConversionModalComponent],
  imports: [
    SharedUiModule,
    UomConversionRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class UomConversionModule {}
