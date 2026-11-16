import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModalModule } from '../share/loading-modal/loading-modal.module';
import { LoadingModule } from '../share/loading/loading.module';
import { ContractMealModalComponent } from '../contract/contract-meal-modal/contract-meal-modal.component';
import { ContractMealRoutingModule } from './contract-meal-routing.module';
import { ContractMealComponent } from './contract-meal.component';

@NgModule({
  declarations: [ContractMealComponent],
  imports: [
    SharedUiModule,
    ContractMealRoutingModule,
    LoadingModule,
    NotFoundModule,
    LoadingModalModule,
    ContractMealModalComponent,
  ],
})
export class ContractMealModule {}
