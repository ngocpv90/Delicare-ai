import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../../share/loading/loading.module';
import { MdUnitOfMeasureModalComponent } from './md-unit-of-measure-modal/md-unit-of-measure-modal.component';
import { MdUnitOfMeasureRoutingModule } from './md-unit-of-measure-routing.module';
import { MdUnitOfMeasureComponent } from './md-unit-of-measure.component';

@NgModule({
  declarations: [MdUnitOfMeasureComponent, MdUnitOfMeasureModalComponent],
  imports: [
    SharedUiModule,
    MdUnitOfMeasureRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
  ],
})
export class MdUnitOfMeasureModule {}
