import { NgModule } from '@angular/core';

import { NotFoundModule } from 'src/app/not-found/not-found.module';
import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModule } from '../share/loading/loading.module';
import { StandardCostRoutingModule } from './standard-cost-routing.module';
import { StandardCostComponent } from './standard-cost.component';

@NgModule({
  declarations: [StandardCostComponent],
  imports: [SharedUiModule, StandardCostRoutingModule, LoadingModule, NotFoundModule],
})
export class StandardCostModule {}
