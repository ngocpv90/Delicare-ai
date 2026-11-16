import { NgModule } from '@angular/core';

import { SharedUiModule } from 'src/app/shared/shared-ui.module';
import { LoadingModule } from '../share/loading/loading.module';
import { MasterImportRoutingModule } from './master-import-routing.module';
import { MasterImportComponent } from './master-import.component';

@NgModule({
  declarations: [MasterImportComponent],
  imports: [SharedUiModule, MasterImportRoutingModule, LoadingModule],
})
export class MasterImportModule {}
