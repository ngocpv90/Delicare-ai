import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { ContractMealModalComponent } from './contract-meal-modal/contract-meal-modal.component';
import { ContractModalComponent } from './contract-modal/contract-modal.component';
import { ContractRoutingModule } from './contract-routing.module';
import { ContractComponent } from './contract.component';

@NgModule({
  declarations: [ContractComponent, ContractModalComponent],
  imports: [
    SharedUiModule,
    ContractRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
    ContractMealModalComponent,
  ],
})
export class ContractModule {}
