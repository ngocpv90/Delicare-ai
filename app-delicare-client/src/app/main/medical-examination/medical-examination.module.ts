import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import {GenericFilterPopupComponent } from 'src/app/shared/generic-filter-popup/generic-filter-popup.component';
import { MedicalExaminationModalComponent } from './medical-examination-modal/medical-examination-modal.component';
import { MedicalExaminationRoutingModule } from './medical-examination-routing.module';
import { MedicalExaminationComponent } from './medical-examination.component';

@NgModule({
  declarations: [MedicalExaminationComponent, MedicalExaminationModalComponent],
  imports: [
    SharedUiModule,
    MedicalExaminationRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
    GenericFilterPopupComponent,
  ],
})
export class MedicalExaminationModule {}
